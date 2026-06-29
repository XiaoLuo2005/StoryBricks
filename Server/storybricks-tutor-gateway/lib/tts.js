const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFile } = require("child_process");
const { promisify } = require("util");
const {
  DASH_KEY,
  DASH_TTS_MODEL,
  DASH_TTS_VOICE,
  DASH_TTS_SAMPLE_RATE,
  TTS_PROVIDER,
  EDGE_TTS_VOICE,
  hasDashScope,
} = require("./config");

const execFileAsync = promisify(execFile);

/** edge-tts-universal 需要完整 Voice 名，或 zh-CN-XiaoxiaoNeural 短名。 */
function normalizeEdgeVoice(voice) {
  const v = (voice || "").trim();
  if (!v) return "Microsoft Server Speech Text to Speech Voice (zh-CN, XiaoxiaoNeural)";
  if (v.includes("Microsoft Server Speech")) return v;
  const m = v.match(/^([a-z]{2}-[A-Z]{2})-(.+)$/);
  if (m) return `Microsoft Server Speech Text to Speech Voice (${m[1]}, ${m[2]})`;
  return v;
}

async function dashTtsToWavBase64(inputText) {
  if (!hasDashScope()) throw new Error("未配置 DASHSCOPE_API_KEY，无法使用 DashScope TTS");

  const payload = {
    model: DASH_TTS_MODEL,
    input: {
      text: inputText.slice(0, 2000),
      voice: DASH_TTS_VOICE,
      format: "wav",
      sample_rate: DASH_TTS_SAMPLE_RATE,
    },
  };

  const r = await fetch("https://dashscope.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${DASH_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

  const raw = await r.text();
  if (!r.ok) throw new Error(`CosyVoice HTTP ${r.status}: ${raw.slice(0, 800)}`);

  const data = JSON.parse(raw);
  const audioUrl = data?.output?.audio?.url;
  if (!audioUrl) throw new Error(`CosyVoice 无音频 URL: ${raw.slice(0, 400)}`);

  const wavR = await fetch(audioUrl);
  if (!wavR.ok) throw new Error(`下载 TTS 音频失败 HTTP ${wavR.status}`);
  const buf = Buffer.from(await wavR.arrayBuffer());
  return { audioBase64: buf.toString("base64"), audioFormat: "wav" };
}

async function edgeTtsToWavBase64(inputText) {
  const { EdgeTTS } = await import("edge-tts-universal");
  const voice = normalizeEdgeVoice(EDGE_TTS_VOICE);
  const tts = new EdgeTTS(inputText.slice(0, 2000), voice);
  const { audio } = await tts.synthesize();
  if (!audio) throw new Error("Edge TTS 未返回音频");

  const mp3 = Buffer.from(await audio.arrayBuffer());
  if (mp3.length === 0) throw new Error("Edge TTS 音频为空");
  const tmpDir = os.tmpdir();
  const mp3Path = path.join(tmpDir, `sb-tts-${Date.now()}.mp3`);
  const wavPath = path.join(tmpDir, `sb-tts-${Date.now()}.wav`);
  fs.writeFileSync(mp3Path, mp3);

  try {
    await execFileAsync("ffmpeg", ["-y", "-i", mp3Path, "-ar", "24000", "-ac", "1", wavPath], {
      timeout: 30000,
    });
    const wav = fs.readFileSync(wavPath);
    return { audioBase64: wav.toString("base64"), audioFormat: "wav" };
  } catch (e) {
    return { audioBase64: mp3.toString("base64"), audioFormat: "mp3" };
  } finally {
    for (const p of [mp3Path, wavPath]) {
      try {
        if (fs.existsSync(p)) fs.unlinkSync(p);
      } catch {
        /* ignore */
      }
    }
  }
}

async function ttsToAudioBase64(inputText) {
  const provider = TTS_PROVIDER === "dash" && hasDashScope() ? "dash" : "edge";
  if (provider === "dash") {
    try {
      return await dashTtsToWavBase64(inputText);
    } catch (e) {
      console.warn("[tts] DashScope 失败，回退 Edge TTS:", e.message);
      return edgeTtsToWavBase64(inputText);
    }
  }
  return edgeTtsToWavBase64(inputText);
}

/** 教程/实时场景：有 Dash 时优先 CosyVoice（比 Edge+ffmpeg 快很多）。 */
async function ttsToAudioBase64Fast(inputText) {
  if (hasDashScope()) {
    try {
      return await dashTtsToWavBase64(inputText);
    } catch (e) {
      console.warn("[tts] fast dash 失败，回退:", e.message);
    }
  }
  return ttsToAudioBase64(inputText);
}

module.exports = { ttsToAudioBase64, ttsToAudioBase64Fast, dashTtsToWavBase64 };
