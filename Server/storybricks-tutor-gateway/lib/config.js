require("dotenv").config({ path: require("path").join(__dirname, "..", ".env") });

const DEEPSEEK_API_KEY = (process.env.DEEPSEEK_API_KEY || "").trim();
const DEEPSEEK_BASE_URL = (process.env.DEEPSEEK_BASE_URL || "https://api.deepseek.com").replace(/\/$/, "");
const DEEPSEEK_MODEL = (process.env.DEEPSEEK_MODEL || "deepseek-chat").trim();

const DASH_KEY = (process.env.DASHSCOPE_API_KEY || "").trim();
const DASH_COMPAT = (process.env.DASHSCOPE_COMPAT_BASE || "https://dashscope.aliyuncs.com/compatible-mode/v1").replace(/\/$/, "");
const DASH_ASR_MODEL = process.env.DASHSCOPE_ASR_MODEL || "qwen3-asr-flash";
const DASH_CHAT_MODEL = process.env.DASHSCOPE_CHAT_MODEL || "qwen-turbo";
const DASH_TTS_MODEL = process.env.DASHSCOPE_TTS_MODEL || "cosyvoice-v3-flash";
const DASH_TTS_VOICE = process.env.DASHSCOPE_TTS_VOICE || "longanyang";
const DASH_TTS_SAMPLE_RATE = Number(process.env.DASHSCOPE_TTS_SAMPLE_RATE || 24000);

/** edge=微软 Edge 免费 TTS（无需卡密）；dash=阿里云 CosyVoice */
const TTS_PROVIDER = (process.env.TTS_PROVIDER || (DASH_KEY ? "dash" : "edge")).toLowerCase();
/** local=本机 Whisper；dash=灵积 ASR；auto=有 Dash 用 dash 否则 local */
const ASR_PROVIDER = (process.env.ASR_PROVIDER || "auto").toLowerCase();
const EDGE_TTS_VOICE = process.env.EDGE_TTS_VOICE || "zh-CN-XiaoxiaoNeural";

const PORT = Number(process.env.PORT || 8787);

function hasDeepSeek() {
  if (!DEEPSEEK_API_KEY) return false;
  if (!/^[\x00-\x7F]+$/.test(DEEPSEEK_API_KEY)) return false;
  return DEEPSEEK_API_KEY.startsWith("sk-");
}

function hasDashScope() {
  return Boolean(DASH_KEY);
}

function storyCreationReady() {
  return hasDeepSeek() || hasDashScope();
}

module.exports = {
  DEEPSEEK_API_KEY,
  DEEPSEEK_BASE_URL,
  DEEPSEEK_MODEL,
  DASH_KEY,
  DASH_COMPAT,
  DASH_ASR_MODEL,
  DASH_CHAT_MODEL,
  DASH_TTS_MODEL,
  DASH_TTS_VOICE,
  DASH_TTS_SAMPLE_RATE,
  TTS_PROVIDER,
  ASR_PROVIDER,
  EDGE_TTS_VOICE,
  PORT,
  hasDeepSeek,
  hasDashScope,
  storyCreationReady,
};
