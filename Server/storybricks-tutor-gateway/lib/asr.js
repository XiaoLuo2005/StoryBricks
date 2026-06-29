const {
  DASH_KEY,
  DASH_COMPAT,
  DASH_ASR_MODEL,
  ASR_PROVIDER,
  hasDashScope,
} = require("./config");
const { refineChildAnswer, refineStoryCreationAnswer, hasDeepSeek } = require("./deepseek");

let whisperPipeline = null;
let whisperLoading = null;

async function dashCompatFetch(path, body) {
  const url = `${DASH_COMPAT}${path.startsWith("/") ? path : `/${path}`}`;
  const r = await fetch(url, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${DASH_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });
  const text = await r.text();
  return { ok: r.ok, status: r.status, text };
}

async function dashTranscribe(buffer) {
  const b64 = buffer.toString("base64");
  const dataUri = `data:audio/wav;base64,${b64}`;
  const body = {
    model: DASH_ASR_MODEL,
    messages: [
      {
        role: "user",
        content: [{ type: "input_audio", input_audio: { data: dataUri } }],
      },
    ],
    stream: false,
    asr_options: { language: "zh", enable_itn: true },
  };
  const { ok, status, text } = await dashCompatFetch("/chat/completions", body);
  if (!ok) throw new Error(`DashScope ASR HTTP ${status}: ${text.slice(0, 1200)}`);
  const data = JSON.parse(text);
  return (data?.choices?.[0]?.message?.content || "").trim();
}

async function getWhisperPipeline() {
  if (whisperPipeline) return whisperPipeline;
  if (!whisperLoading) {
    whisperLoading = (async () => {
      const { pipeline } = await import("@xenova/transformers");
      console.log("[asr] 首次加载本机 Whisper 模型，请稍候…");
      whisperPipeline = await pipeline("automatic-speech-recognition", "Xenova/whisper-small");
      console.log("[asr] Whisper 模型就绪");
      return whisperPipeline;
    })();
  }
  return whisperLoading;
}

async function localWhisperTranscribe(buffer) {
  const { WaveFile } = require("wavefile");
  const transcriber = await getWhisperPipeline();
  const wav = new WaveFile(buffer);
  wav.toBitDepth("32f");
  wav.toSampleRate(16000);
  let audioData = wav.getSamples();
  if (Array.isArray(audioData)) {
    if (audioData.length > 1) {
      const SCALING_FACTOR = Math.sqrt(2);
      for (let i = 0; i < audioData[0].length; ++i) {
        audioData[0][i] = (SCALING_FACTOR * (audioData[0][i] + audioData[1][i])) / 2;
      }
    }
    audioData = audioData[0];
  }
  const result = await transcriber(audioData, {
    language: "chinese",
    task: "transcribe",
    chunk_length_s: 20,
    stride_length_s: 5,
  });
  return String(result?.text || "").trim();
}

async function transcribeWavBuffer(buffer, fields = {}) {
  let provider = ASR_PROVIDER;
  if (provider === "auto") provider = hasDashScope() ? "dash" : "local";

  let transcript = "";
  if (provider === "dash" && hasDashScope()) {
    try {
      transcript = await dashTranscribe(buffer);
    } catch (e) {
      console.warn("[asr] DashScope ASR 失败，回退本机 Whisper:", e.message);
      transcript = await localWhisperTranscribe(buffer);
    }
  } else {
    transcript = await localWhisperTranscribe(buffer);
  }

  if (!transcript) return { transcript: "", rawTranscript: "" };

  if (hasDeepSeek() && String(fields.refineWithDeepSeek || "true").toLowerCase() !== "false") {
    try {
      const refined = await refineChildAnswer(transcript, fields);
      return { transcript: refined || transcript, rawTranscript: transcript };
    } catch (e) {
      console.warn("[asr] DeepSeek 整理回答失败，使用原识别文本:", e.message);
    }
  }

  return { transcript, rawTranscript: transcript };
}

/** 故事创作专用：ASR 后可选 DeepSeek 润色。fast=true 时直接返回识别原文。 */
async function transcribeStoryCreationWavBuffer(buffer, fields = {}) {
  const fast = String(fields.fast || "").toLowerCase() === "true";
  const { transcript: raw } = await transcribeWavBuffer(buffer, { refineWithDeepSeek: "false" });
  if (!raw) return { transcript: "", rawTranscript: "" };

  if (fast || !hasDeepSeek()) {
    return { transcript: raw, rawTranscript: raw };
  }

  try {
    const refined = await refineStoryCreationAnswer(raw, fields);
    return { transcript: refined || raw, rawTranscript: raw };
  } catch (e) {
    console.warn("[asr] 故事创作 DeepSeek 润色失败，使用 ASR 原文:", e.message);
  }

  return { transcript: raw, rawTranscript: raw };
}

module.exports = { transcribeWavBuffer, transcribeStoryCreationWavBuffer };
