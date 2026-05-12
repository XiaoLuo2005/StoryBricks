/**
 * StoryBricks 教程 AI 助教网关（Node 内置 http + busboy）
 *
 * 仅对接阿里云灵积 DashScope：
 * - 语音识别：兼容模式 qwen3-asr-flash（WAV Base64 Data URL）
 * - 对话：兼容模式通义（如 qwen-turbo）
 * - 语音合成：CosyVoice HTTP，拉取 wav URL 后转 base64 给 Unity
 *
 * - GET  /health
 * - POST /api/tutor/text   JSON（可选：tutorialTutorOverview、stepGoal、stepPartsUsed、stepKeyActions、stepPitfalls）
 * - POST /api/tutor/voice  multipart/form-data，字段 audio（WAV）+ 同上文本字段
 *
 * 必填环境变量：DASHSCOPE_API_KEY
 */
const http = require("http");
const Busboy = require("busboy");

const PORT = Number(process.env.PORT || 8787);

const DASH_KEY = process.env.DASHSCOPE_API_KEY || "";
const DASH_COMPAT = (process.env.DASHSCOPE_COMPAT_BASE || "https://dashscope.aliyuncs.com/compatible-mode/v1").replace(/\/$/, "");
const DASH_ASR_MODEL = process.env.DASHSCOPE_ASR_MODEL || "qwen3-asr-flash";
const DASH_CHAT_MODEL = process.env.DASHSCOPE_CHAT_MODEL || "qwen-turbo";
const DASH_TTS_MODEL = process.env.DASHSCOPE_TTS_MODEL || "cosyvoice-v3-flash";
const DASH_TTS_VOICE = process.env.DASHSCOPE_TTS_VOICE || "longanyang";
const DASH_TTS_SAMPLE_RATE = Number(process.env.DASHSCOPE_TTS_SAMPLE_RATE || 24000);

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
  });
  res.end(body);
}

function readJsonBody(req, maxBytes = 512 * 1024) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let len = 0;
    req.on("data", (c) => {
      len += c.length;
      if (len > maxBytes) {
        reject(new Error("body too large"));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on("end", () => {
      try {
        const raw = Buffer.concat(chunks).toString("utf8");
        resolve(raw ? JSON.parse(raw) : {});
      } catch (e) {
        reject(e);
      }
    });
    req.on("error", reject);
  });
}

function parseVoiceMultipart(req) {
  return new Promise((resolve, reject) => {
    const fields = {};
    let audioBuffer = null;

    const bb = Busboy({
      headers: req.headers,
      limits: { fileSize: 12 * 1024 * 1024 },
    });

    bb.on("field", (name, val) => {
      fields[name] = val;
    });

    bb.on("file", (name, file, info) => {
      if (name !== "audio") {
        file.resume();
        return;
      }
      const chunks = [];
      file.on("data", (d) => chunks.push(d));
      file.on("limit", () => reject(new Error("audio file too large")));
      file.on("end", () => {
        audioBuffer = Buffer.concat(chunks);
      });
    });

    bb.on("finish", () => resolve({ fields, audioBuffer }));
    bb.on("error", reject);
    req.pipe(bb);
  });
}

function dashKeyMissingResponse() {
  return {
    status: 503,
    body: {
      error: "Set DASHSCOPE_API_KEY",
      reply: "",
      transcript: "",
      audioBase64: "",
    },
  };
}

async function dashCompatFetch(path, body) {
  const url = `${DASH_COMPAT}${path.startsWith("/") ? path : "/" + path}`;
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

const OVERVIEW_MAX_CHARS = 12000;

function sliceOverview(raw) {
  const t = String(raw || "").trim();
  if (!t) return "";
  if (t.length <= OVERVIEW_MAX_CHARS) return t;
  return `${t.slice(0, OVERVIEW_MAX_CHARS)}\n…(总览已截断)`;
}

function formatStructuredStep(body) {
  const goal = String(body.stepGoal || "").trim();
  const parts = String(body.stepPartsUsed || "").trim();
  const actions = String(body.stepKeyActions || "").trim();
  const pit = String(body.stepPitfalls || "").trim();
  if (!goal && !parts && !actions && !pit) return "";
  let s =
    "本步结构化说明（由教程作者提供；讲解时优先据此；与屏幕步骤图明显冲突时以步骤图为准）：\n";
  if (goal) s += `【本步目标】${goal}\n`;
  if (parts) s += `【涉及积木/零件】\n${parts}\n`;
  if (actions) s += `【关键动作】\n${actions}\n`;
  if (pit) s += `【易错与安全】\n${pit}\n`;
  return `${s}\n`;
}

function buildSystemPrompt(body) {
  const title = body.tutorialTitle || "积木拼装教程";
  const stepIndex = Number(body.stepIndex) || 0;
  const stepCount = Number(body.stepCount) || 1;
  const overview = sliceOverview(body.tutorialTutorOverview);
  const overviewBlock = overview ? `【教程总览（配套文档）】\n${overview}\n\n` : "";
  const structuredBlock = formatStructuredStep(body);
  const hint = (body.stepHint || "").trim();
  const hintBlock = hint ? `本步简短提示（可与上文并用；以步骤图为准，勿编造细节）：\n${hint}\n` : "";
  return `你是儿童积木拼装教程里的「语音小助教」，昵称友好、句子短（每次回答控制在 2～5 句中文口语）。
规则：
- 只围绕当前教程「${title}」与拼装步骤回答问题；拒绝无关话题与危险操作。
- 若上文包含「教程总览」或「本步结构化说明」，必须在其范围内讲解；不要编造未出现的零件编号或步骤图上未体现的具体孔位。
- 具体卡扣位置、孔位若说明与步骤图均未写明，不要编造；请引导孩子对照屏幕上的步骤图、必要时用「上一页/下一页」回看。
${overviewBlock}${structuredBlock}${hintBlock}
当前进度：第 ${stepIndex + 1} 步，共 ${stepCount} 步。
语气：耐心、鼓励，不要说教；可建议「轻轻对齐」「试着转一下角度」「先找颜色/形状相同的那一块」。`;
}

async function runChat(systemPrompt, userText) {
  const payload = {
    model: DASH_CHAT_MODEL,
    messages: [
      { role: "system", content: systemPrompt },
      { role: "user", content: userText },
    ],
    temperature: 0.6,
    max_tokens: 640,
  };
  const { ok, status, text } = await dashCompatFetch("/chat/completions", payload);
  if (!ok) throw new Error(`dashscope chat HTTP ${status}: ${text.slice(0, 800)}`);
  const data = JSON.parse(text);
  const reply = data?.choices?.[0]?.message?.content?.trim() || "";
  if (!reply) throw new Error("chat empty reply");
  return reply;
}

async function transcribeFromWavBuffer(buffer) {
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
    asr_options: {
      language: "zh",
      enable_itn: true,
    },
  };
  const { ok, status, text } = await dashCompatFetch("/chat/completions", body);
  if (!ok) throw new Error(`dashscope asr HTTP ${status}: ${text.slice(0, 1200)}`);
  const data = JSON.parse(text);
  return (data?.choices?.[0]?.message?.content || "").trim();
}

async function ttsToWavBase64(inputText) {
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
  if (!r.ok) throw new Error(`cosyvoice HTTP ${r.status}: ${raw.slice(0, 800)}`);

  const data = JSON.parse(raw);
  const audioUrl = data?.output?.audio?.url;
  if (!audioUrl) throw new Error(`cosyvoice no audio url: ${raw.slice(0, 400)}`);

  const wavR = await fetch(audioUrl);
  if (!wavR.ok) throw new Error(`download wav HTTP ${wavR.status}`);
  const buf = Buffer.from(await wavR.arrayBuffer());
  return buf.toString("base64");
}

const server = http.createServer(async (req, res) => {
  const u = new URL(req.url || "/", `http://${req.headers.host || "127.0.0.1"}`);
  const path = u.pathname;

  if (req.method === "OPTIONS") {
    res.writeHead(204, {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
    });
    res.end();
    return;
  }

  if (req.method === "GET" && path === "/health") {
    sendJson(res, 200, {
      ok: true,
      hasDashScopeKey: Boolean(DASH_KEY),
      dashCompatBase: DASH_COMPAT,
    });
    return;
  }

  if (!DASH_KEY && (path === "/api/tutor/text" || path === "/api/tutor/voice")) {
    const err = dashKeyMissingResponse();
    sendJson(res, err.status, err.body);
    return;
  }

  try {
    if (req.method === "POST" && path === "/api/tutor/text") {
      const body = await readJsonBody(req);
      const um = ((body && body.userMessage) || "").trim();
      if (!um) {
        sendJson(res, 400, { error: "userMessage required", reply: "" });
        return;
      }
      const systemPrompt = buildSystemPrompt(body || {});
      const reply = await runChat(systemPrompt, um);
      sendJson(res, 200, { reply, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/tutor/voice") {
      const ct = (req.headers["content-type"] || "").toLowerCase();
      if (!ct.includes("multipart/form-data")) {
        sendJson(res, 400, {
          transcript: "",
          reply: "",
          audioBase64: "",
          error: "Content-Type must be multipart/form-data",
        });
        return;
      }

      const { fields, audioBuffer } = await parseVoiceMultipart(req);
      if (!audioBuffer || audioBuffer.length === 0) {
        sendJson(res, 400, {
          transcript: "",
          reply: "",
          audioBase64: "",
          error: "missing audio file field",
        });
        return;
      }

      const transcript = await transcribeFromWavBuffer(audioBuffer);
      if (!transcript) {
        sendJson(res, 400, {
          transcript: "",
          reply: "",
          audioBase64: "",
          error: "empty transcription",
        });
        return;
      }

      const systemPrompt = buildSystemPrompt({
        tutorialTitle: fields.tutorialTitle,
        stepIndex: fields.stepIndex,
        stepCount: fields.stepCount,
        stepHint: fields.stepHint,
        tutorialTutorOverview: fields.tutorialTutorOverview,
        stepGoal: fields.stepGoal,
        stepPartsUsed: fields.stepPartsUsed,
        stepKeyActions: fields.stepKeyActions,
        stepPitfalls: fields.stepPitfalls,
      });
      const reply = await runChat(systemPrompt, transcript);
      const audioBase64 = await ttsToWavBase64(reply);
      sendJson(res, 200, { transcript, reply, audioBase64, error: "" });
      return;
    }

    sendJson(res, 404, { error: "not found" });
  } catch (e) {
    console.error(e);
    if (path === "/api/tutor/voice") {
      sendJson(res, 500, {
        transcript: "",
        reply: "",
        audioBase64: "",
        error: String(e.message || e),
      });
    } else if (path === "/api/tutor/text") {
      sendJson(res, 500, { reply: "", error: String(e.message || e) });
    } else {
      sendJson(res, 500, { error: String(e.message || e) });
    }
  }
});

server.listen(PORT, () => {
  console.log(`StoryBricks tutor gateway (DashScope) http://127.0.0.1:${PORT}`);
  console.log(`DASH_COMPAT=${DASH_COMPAT} hasDashKey=${Boolean(DASH_KEY)}`);
});
