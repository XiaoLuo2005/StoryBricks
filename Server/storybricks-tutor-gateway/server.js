/**
 * StoryBricks 语音 / AI 网关
 *
 * 故事创作（推荐仅配 DEEPSEEK_API_KEY）：
 * - 提问：DeepSeek 生成儿童化话术
 * - TTS：Edge 免费语音（或 DashScope CosyVoice）
 * - ASR：本机 Whisper + DeepSeek 整理孩子回答
 *
 * 教程助教（可选 DASHSCOPE_API_KEY）：/api/tutor/*
 */
require("dotenv").config();

const http = require("http");
const Busboy = require("busboy");
const cfg = require("./lib/config");
const deepseek = require("./lib/deepseek");
const { ttsToAudioBase64, ttsToAudioBase64Fast } = require("./lib/tts");
const { transcribeWavBuffer, transcribeStoryCreationWavBuffer } = require("./lib/asr");

const PORT = cfg.PORT;
const DASH_KEY = cfg.DASH_KEY;
const DASH_COMPAT = cfg.DASH_COMPAT;
const DASH_ASR_MODEL = cfg.DASH_ASR_MODEL;
const DASH_CHAT_MODEL = cfg.DASH_CHAT_MODEL;

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

function beginNdjson(res) {
  res.writeHead(200, {
    "Content-Type": "application/x-ndjson; charset=utf-8",
    "Transfer-Encoding": "chunked",
    "Cache-Control": "no-cache",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
  });
}

function writeNdjson(res, obj) {
  res.write(`${JSON.stringify(obj)}\n`);
}

function buildTutorVoiceContext(fields) {
  return buildSystemPrompt(
    {
      tutorialTitle: fields.tutorialTitle,
      stepIndex: fields.stepIndex,
      stepCount: fields.stepCount,
      stepHint: fields.stepHint,
      tutorialTutorOverview: fields.tutorialTutorOverview,
      stepGoal: fields.stepGoal,
      stepPartsUsed: fields.stepPartsUsed,
      stepKeyActions: fields.stepKeyActions,
      stepPitfalls: fields.stepPitfalls,
    },
    { compact: true },
  );
}

async function runTutorVoicePipeline(fields, audioBuffer) {
  const t0 = Date.now();
  const transcript = await transcribeFromWavBuffer(audioBuffer);
  const tAsr = Date.now();
  if (!transcript) throw new Error("empty transcription");

  const systemPrompt = buildTutorVoiceContext(fields);
  const reply = await runTutorChat(systemPrompt, transcript);
  const tChat = Date.now();
  const { audioBase64, audioFormat } = await ttsToWavBase64(reply);
  const tTts = Date.now();
  const timing = {
    asr: tAsr - t0,
    chat: tChat - tAsr,
    tts: tTts - tChat,
    total: tTts - t0,
  };
  console.log(
    `[tutor/voice] asr=${timing.asr}ms chat=${timing.chat}ms tts=${timing.tts}ms total=${timing.total}ms`,
  );
  return { transcript, reply, audioBase64, audioFormat: audioFormat || "wav", timing };
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

function storyKeyMissingResponse() {
  return {
    status: 503,
    body: {
      error: "请配置 DEEPSEEK_API_KEY（复制 .env.example 为 .env 并填写卡密）",
      audioBase64: "",
      transcript: "",
      questions: [],
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
const TUTOR_OVERVIEW_MAX_CHARS = 2500;

function sliceOverview(raw, maxChars = OVERVIEW_MAX_CHARS) {
  const t = String(raw || "").trim();
  if (!t) return "";
  if (t.length <= maxChars) return t;
  return `${t.slice(0, maxChars)}\n…(总览已截断)`;
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

function formatStructuredStepCompact(body) {
  const goal = String(body.stepGoal || "").trim();
  const actions = String(body.stepKeyActions || "").trim();
  if (!goal && !actions) return "";
  return `本步要点：${goal}${actions ? `；${actions}` : ""}\n`;
}

function buildSystemPrompt(body, { compact = false } = {}) {
  const title = body.tutorialTitle || "积木拼装教程";
  const stepIndex = Number(body.stepIndex) || 0;
  const stepCount = Number(body.stepCount) || 1;
  const overview = sliceOverview(
    body.tutorialTutorOverview,
    compact ? TUTOR_OVERVIEW_MAX_CHARS : OVERVIEW_MAX_CHARS,
  );
  const overviewBlock = overview ? `【教程总览】\n${overview}\n\n` : "";
  const structuredBlock = compact ? formatStructuredStepCompact(body) : formatStructuredStep(body);
  const hint = (body.stepHint || "").trim();
  const hintBlock = hint ? `本步提示：${hint}\n` : "";
  if (compact) {
    return `你是教程语音助手「乐乐」，用 2～3 句口语直接回答。
- 只讲「${title}」第 ${stepIndex + 1}/${stepCount} 步；不编造零件/孔位；不要重复唤醒词。
${overviewBlock}${structuredBlock}${hintBlock}`;
  }
  return `你是儿童积木拼装教程里的语音助手「乐乐」，自称「乐乐」，句子短（每次回答控制在 2～5 句中文口语）。
规则：
- 只围绕当前教程「${title}」与拼装步骤回答问题；拒绝无关话题与危险操作。
- 孩子已经通过语音唤醒你；直接回答问题，不要在回复里重复「你好乐乐」等唤醒词。
- 若上文包含「教程总览」或「本步结构化说明」，必须在其范围内讲解；不要编造未出现的零件编号或步骤图上未体现的具体孔位。
- 具体卡扣位置、孔位若说明与步骤图均未写明，不要编造；请引导孩子对照屏幕上的步骤图、必要时用「上一页/下一页」回看。
${overviewBlock}${structuredBlock}${hintBlock}
当前进度：第 ${stepIndex + 1} 步，共 ${stepCount} 步。
语气：耐心、鼓励，不要说教；可建议「轻轻对齐」「试着转一下角度」「先找颜色/形状相同的那一块」。`;
}

async function runTutorChat(systemPrompt, userText) {
  if (deepseek.hasDeepSeek()) {
    return deepseek.deepseekChat(
      [
        { role: "system", content: systemPrompt },
        { role: "user", content: userText },
      ],
      { temperature: 0.45, max_tokens: 160 },
    );
  }

  if (!DASH_KEY) throw new Error("请配置 DEEPSEEK_API_KEY 或 DASHSCOPE_API_KEY");

  const payload = {
    model: DASH_CHAT_MODEL,
    messages: [
      { role: "system", content: systemPrompt },
      { role: "user", content: userText },
    ],
    temperature: 0.45,
    max_tokens: 200,
  };
  const { ok, status, text } = await dashCompatFetch("/chat/completions", payload);
  if (!ok) throw new Error(`dashscope chat HTTP ${status}: ${text.slice(0, 800)}`);
  const data = JSON.parse(text);
  const reply = data?.choices?.[0]?.message?.content?.trim() || "";
  if (!reply) throw new Error("chat empty reply");
  return reply;
}

async function runChat(systemPrompt, userText) {
  if (deepseek.hasDeepSeek()) {
    return deepseek.deepseekChat(
      [
        { role: "system", content: systemPrompt },
        { role: "user", content: userText },
      ],
      { temperature: 0.6, max_tokens: 320 },
    );
  }

  if (!DASH_KEY) throw new Error("请配置 DEEPSEEK_API_KEY 或 DASHSCOPE_API_KEY");

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
  const { transcript } = await transcribeWavBuffer(buffer, { refineWithDeepSeek: "false" });
  return transcript;
}

async function ttsToWavBase64(inputText) {
  return ttsToAudioBase64Fast(inputText);
}

async function buildStoryCreationQuestions(body) {
  if (deepseek.hasDeepSeek()) return deepseek.buildStoryCreationQuestions(body);

  const gaps = Array.isArray(body.gaps) ? body.gaps : [];
  if (gaps.length === 0) return [];
  if (!cfg.hasDashScope()) throw new Error("请配置 DEEPSEEK_API_KEY");

  const systemPrompt = buildStoryCreationQuestionPrompt(body);
  const payload = {
    model: DASH_CHAT_MODEL,
    messages: [
      { role: "system", content: systemPrompt },
      { role: "user", content: "请为上述每个缺口各生成一条儿童化语音提问。" },
    ],
    temperature: 0.65,
    max_tokens: 1200,
  };
  const { ok, status, text } = await dashCompatFetch("/chat/completions", payload);
  if (!ok) throw new Error(`dashscope questions HTTP ${status}: ${text.slice(0, 800)}`);
  const data = JSON.parse(text);
  const raw = (data?.choices?.[0]?.message?.content || "").trim();
  if (!raw) throw new Error("questions empty reply");

  const jsonStart = raw.indexOf("[");
  const jsonEnd = raw.lastIndexOf("]");
  const slice = jsonStart >= 0 && jsonEnd > jsonStart ? raw.slice(jsonStart, jsonEnd + 1) : raw;
  const parsed = JSON.parse(slice);
  if (!Array.isArray(parsed)) throw new Error("questions not array");

  return parsed
    .filter((q) => q && String(q.text || "").trim())
    .map((q, i) => ({
      id: String(q.id || `gap_${i}`).trim(),
      text: String(q.text).trim(),
    }));
}

async function refineStoryCreationImagePrompt(body) {
  if (deepseek.hasDeepSeek()) return deepseek.buildStoryCreationImagePrompt(body);
  if (!cfg.hasDashScope()) throw new Error("请配置 DEEPSEEK_API_KEY");

  const messages = deepseek.buildStoryCreationImagePromptMessages(body);
  const payload = {
    model: DASH_CHAT_MODEL,
    messages,
    temperature: 0.45,
    max_tokens: 600,
  };
  const { ok, status, text } = await dashCompatFetch("/chat/completions", payload);
  if (!ok) throw new Error(`dashscope refine-prompt HTTP ${status}: ${text.slice(0, 800)}`);
  const data = JSON.parse(text);
  const raw = (data?.choices?.[0]?.message?.content || "").trim();
  if (!raw) throw new Error("refine-prompt empty reply");
  return raw.replace(/^["'「]|["'」]$/g, "").trim();
}

async function extractPageStory(body) {
  if (deepseek.hasDeepSeek()) {
    try {
      return await deepseek.buildStoryCreationExtractPageStory(body);
    } catch (e) {
      console.warn("[extract-page-story] DeepSeek 失败，使用本地回退:", e.message);
    }
  }

  const log = String(body.conversationLog || "").trim();
  const scene = String(body.sceneGuideText || "").trim();
  const placement = String(body.arucoPlacement || "").trim();
  let voiceSupplement = log.length > 0 ? log.replace(/\r/g, "").slice(0, 400) : scene;
  if (placement) {
    voiceSupplement = voiceSupplement
      ? `${voiceSupplement}；${placement}`
      : placement;
  }
  const recapLine = voiceSupplement
    ? `我听说是：${voiceSupplement.slice(0, 120)}。`
    : `这一页是${body.pageTitle || "故事"}，${scene || "摆好了我们就去画！"}`;
  const gaps = Array.isArray(body.gaps) ? body.gaps : [];
  const behaviorGap = gaps.find((g) => String(g.kind || "").includes("Behavior"));
  const needMore = log.length < 8 && behaviorGap;
  return {
    voiceSupplement: voiceSupplement || scene,
    recapLine,
    missingField: needMore ? "behavior" : "none",
    followUpQuestion: needMore
      ? `再跟乐乐说说，${behaviorGap.roleName || "小伙伴"}这一页在干什么呀？`
      : "",
    conversationDone: !needMore && Boolean(voiceSupplement || scene),
  };
}

function buildStoryCreationQuestionPrompt(body) {
  const storyTitle = String(body.storyTitle || "故事").trim();
  const pageTitle = String(body.pageTitle || "").trim();
  const scene = String(body.sceneGuideText || "").trim();
  const previous = String(body.previousSummary || "").trim();
  const gaps = Array.isArray(body.gaps) ? body.gaps : [];

  let gapBlock = "";
  gaps.forEach((g, i) => {
    const kind = String(g.kind || "").trim();
    const role = String(g.roleName || "").trim();
    const fb = String(g.fallbackQuestion || "").trim();
    gapBlock += `${i + 1}. 类型=${kind || "未知"}；角色=${role || "无"}；参考话术=${fb || "无"}\n`;
  });

  return `你是 3～8 岁儿童故事创作的语音助手「乐乐」，负责用口语提问补全孩子搭建时缺少的信息。
规则：
- 根据「故事」「本页场景」「前情」「识别缺口」生成提问，每条 2～3 句，亲切、简短，自称「乐乐」，称呼孩子「小朋友」，直接邀请孩子开口回答（不要说唤醒词「你好乐乐」）。
- 缺口类型 CharacterBehavior：本页已识别的角色，问孩子这个角色在做什么、想干什么（行为只靠语音，不从积木识别）。
- 缺口类型 CharacterPosition：多个角色已在镜头里，问谁在前谁在后、离场景中心（大树/终点等）远近，或要不要调整站位。
- 缺口类型 OptionalStoryElement：行为问完后固定追问本页还想加什么；若提供了参考话术，可略作口语化但保持原意。
- 结合前情与场景举例（如龟兔 P2 大树下可问兔子想休息还是玩耍），但不要编造与场景矛盾的剧情。
- 只输出 JSON 数组，不要 markdown，不要解释。格式：[{"id":"gap_0","text":"提问内容"}]，id 按缺口顺序 gap_0、gap_1…
故事：${storyTitle}
本页：${pageTitle}
场景说明：${scene || "（无）"}
前情摘要：${previous || "（首页无前情）"}
识别缺口：
${gapBlock || "（无缺口）"}`;
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
      hasDeepSeekKey: cfg.hasDeepSeek(),
      hasDashScopeKey: cfg.hasDashScope(),
      tutorReady: cfg.hasDeepSeek() || cfg.hasDashScope(),
      storyCreationReady: cfg.storyCreationReady(),
      ttsProvider: cfg.TTS_PROVIDER,
      asrProvider: cfg.ASR_PROVIDER,
      deepseekModel: cfg.DEEPSEEK_MODEL,
      dashCompatBase: DASH_COMPAT,
    });
    return;
  }

  if (!cfg.hasDeepSeek() && !cfg.hasDashScope()) {
    const err = storyKeyMissingResponse();
    sendJson(res, err.status, err.body);
    return;
  }

  if (path.startsWith("/api/story-creation/") && !cfg.storyCreationReady()) {
    const err = storyKeyMissingResponse();
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
      const systemPrompt = buildSystemPrompt(body || {}, { compact: true });
      const reply = await runTutorChat(systemPrompt, um);
      let audioBase64 = "";
      let audioFormat = "wav";
      if (body.includeTts !== false) {
        const tts = await ttsToAudioBase64Fast(reply);
        audioBase64 = tts.audioBase64 || "";
        audioFormat = tts.audioFormat || "wav";
      }
      sendJson(res, 200, { reply, audioBase64, audioFormat, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/tutor/voice-stream") {
      const ct = (req.headers["content-type"] || "").toLowerCase();
      if (!ct.includes("multipart/form-data")) {
        sendJson(res, 400, { error: "Content-Type must be multipart/form-data" });
        return;
      }

      const { fields, audioBuffer } = await parseVoiceMultipart(req);
      if (!audioBuffer || audioBuffer.length === 0) {
        sendJson(res, 400, { error: "missing audio file field" });
        return;
      }

      beginNdjson(res);
      try {
        const t0 = Date.now();
        const transcript = await transcribeFromWavBuffer(audioBuffer);
        if (!transcript) {
          writeNdjson(res, { stage: "error", error: "empty transcription" });
          res.end();
          return;
        }
        writeNdjson(res, { stage: "transcript", transcript, ms: Date.now() - t0 });

        const reply = await runTutorChat(buildTutorVoiceContext(fields), transcript);
        writeNdjson(res, { stage: "reply", reply, ms: Date.now() - t0 });

        const { audioBase64, audioFormat } = await ttsToWavBase64(reply);
        writeNdjson(res, {
          stage: "audio",
          audioBase64,
          audioFormat: audioFormat || "wav",
          ms: Date.now() - t0,
        });
        writeNdjson(res, { stage: "done", ms: Date.now() - t0 });
        console.log(`[tutor/voice-stream] total=${Date.now() - t0}ms`);
        res.end();
      } catch (e) {
        writeNdjson(res, { stage: "error", error: String(e.message || e) });
        res.end();
      }
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

      const { transcript, reply, audioBase64, audioFormat } = await runTutorVoicePipeline(fields, audioBuffer);
      sendJson(res, 200, {
        transcript,
        reply,
        audioBase64,
        audioFormat,
        error: "",
      });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/tts") {
      const body = await readJsonBody(req);
      const text = String((body && body.text) || "").trim();
      if (!text) {
        sendJson(res, 400, { audioBase64: "", audioFormat: "wav", error: "text required" });
        return;
      }
      const { audioBase64, audioFormat } = await ttsToAudioBase64Fast(text);
      sendJson(res, 200, { audioBase64, audioFormat: audioFormat || "wav", error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/asr") {
      const ct = (req.headers["content-type"] || "").toLowerCase();
      if (!ct.includes("multipart/form-data")) {
        sendJson(res, 400, { transcript: "", rawTranscript: "", error: "Content-Type must be multipart/form-data" });
        return;
      }
      const { fields, audioBuffer } = await parseVoiceMultipart(req);
      if (!audioBuffer || audioBuffer.length === 0) {
        sendJson(res, 400, { transcript: "", rawTranscript: "", error: "missing audio file field" });
        return;
      }
      const { transcript, rawTranscript } = await transcribeStoryCreationWavBuffer(audioBuffer, fields || {});
      if (!transcript) {
        sendJson(res, 400, { transcript: "", rawTranscript: rawTranscript || "", error: "empty transcription" });
        return;
      }
      sendJson(res, 200, { transcript, rawTranscript: rawTranscript || transcript, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/questions") {
      const body = await readJsonBody(req);
      const gaps = Array.isArray(body.gaps) ? body.gaps : [];
      if (gaps.length === 0) {
        sendJson(res, 200, { questions: [], error: "" });
        return;
      }
      const questions = await buildStoryCreationQuestions(body);
      sendJson(res, 200, { questions, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/refine-prompt") {
      const body = await readJsonBody(req);
      const prompt = await refineStoryCreationImagePrompt(body || {});
      sendJson(res, 200, { prompt, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/reply") {
      const body = await readJsonBody(req);
      const result = await deepseek.buildStoryCreationReply(body || {});
      sendJson(res, 200, { ...result, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/summary") {
      const body = await readJsonBody(req);
      const summary = await deepseek.buildStoryCreationPageSummary(body || {});
      sendJson(res, 200, { summary: summary || "", error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/page-caption") {
      const body = await readJsonBody(req);
      const caption = await deepseek.buildStoryCreationPageCaption(body || {});
      sendJson(res, 200, { caption: caption || "", error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/free-chat") {
      const body = await readJsonBody(req);
      const um = String((body && body.userMessage) || "").trim();
      if (!um) {
        sendJson(res, 400, { reply: "", error: "userMessage required" });
        return;
      }
      const reply = await deepseek.buildStoryCreationFreeChat(body || {});
      sendJson(res, 200, { reply, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/extract-page-story") {
      const body = await readJsonBody(req);
      const result = await extractPageStory(body || {});
      sendJson(res, 200, { ...result, error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/wait-narration") {
      const body = await readJsonBody(req);
      const narration = await deepseek.buildStoryCreationWaitNarration(body || {});
      sendJson(res, 200, { narration: narration || "", error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/page-recap") {
      const body = await readJsonBody(req);
      const recap = await deepseek.buildStoryCreationPageRecap(body || {});
      sendJson(res, 200, { recap: recap || "", error: "" });
      return;
    }

    if (req.method === "POST" && path === "/api/story-creation/branch-hint") {
      const body = await readJsonBody(req);
      const hint = await deepseek.buildStoryCreationBranchHint(body || {});
      sendJson(res, 200, { hint: hint || "", error: "" });
      return;
    }

    sendJson(res, 404, { error: "not found" });
  } catch (e) {
    console.error(e);
    if (path === "/api/tutor/voice" || path === "/api/tutor/voice-stream") {
      sendJson(res, 500, {
        transcript: "",
        reply: "",
        audioBase64: "",
        error: String(e.message || e),
      });
    } else if (path === "/api/tutor/text") {
      sendJson(res, 500, { reply: "", error: String(e.message || e) });
    } else if (path.startsWith("/api/story-creation/")) {
      sendJson(res, 500, {
        audioBase64: "",
        transcript: "",
        questions: [],
        prompt: "",
        error: String(e.message || e),
      });
    } else {
      sendJson(res, 500, { error: String(e.message || e) });
    }
  }
});

server.listen(PORT, () => {
  console.log(`StoryBricks tutor gateway http://127.0.0.1:${PORT}`);
  console.log(`DeepSeek=${cfg.hasDeepSeek()} model=${cfg.DEEPSEEK_MODEL} DashScope=${cfg.hasDashScope()}`);
  console.log(`TTS=${cfg.TTS_PROVIDER} ASR=${cfg.ASR_PROVIDER}`);
});
