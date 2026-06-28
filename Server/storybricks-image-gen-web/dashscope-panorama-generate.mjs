/**
 * 360 全景图（equirectangular 2:1）生成，复用 wan2.6-image 文生图异步任务。
 */

import { asyncTextToImage, getApiKey } from "./dashscope-generate.mjs";

const PANORAMA_SIZE = (process.env.PANORAMA_SIZE || "1536*768").trim();
const PANORAMA_MODEL = (process.env.PANORAMA_MODEL || process.env.PANORAMA_IMAGE_MODEL || "wan2.6-image").trim();
const PANORAMA_MIN_PIXELS = 589_824;
/** enable_interleave=true（文生单图）时 wan2.6-image 上限约 1280×1280 */
const PANORAMA_MAX_PIXELS = 1_638_400;

const PANORAMA_PREFIX =
  "360 degree equirectangular panorama, seamless wrap-around, immersive environment, " +
  "no black borders, no text, no watermark, 2:1 aspect ratio, ";

function parseSizePair(size) {
  const m = String(size || "")
    .trim()
    .match(/^(\d+)\s*[*xX×]\s*(\d+)$/);
  if (!m) return null;
  return { width: Number(m[1]), height: Number(m[2]) };
}

/** enable_interleave=true 文生图：总像素 ∈ [589824, 1638400]；默认 1536×768。 */
function normalizePanoramaSize(size) {
  const fallback = "1536*768";
  const parsed = parseSizePair(size);
  if (!parsed || parsed.width <= 0 || parsed.height <= 0) return fallback;

  const pixels = parsed.width * parsed.height;
  if (pixels >= PANORAMA_MIN_PIXELS && pixels <= PANORAMA_MAX_PIXELS) {
    return `${parsed.width}*${parsed.height}`;
  }

  console.warn(
    `[panorama] size ${size} (${pixels}px) out of range; using ${fallback}`,
  );
  return fallback;
}

/**
 * @param {Record<string, unknown>} body
 * @returns {Promise<{ status: number, json: Record<string, unknown> }>}
 */
export async function handleGeneratePanorama(body) {
  const apiKey = getApiKey();
  if (!apiKey) {
    return { status: 503, json: { detail: "DASHSCOPE_API_KEY not configured on server" } };
  }

  const scene = String(body?.prompt || "").trim();
  if (!scene) {
    return { status: 400, json: { detail: "prompt is required" } };
  }

  const model = String(body?.model || PANORAMA_MODEL).trim() || PANORAMA_MODEL;
  const size = normalizePanoramaSize(String(body?.size || PANORAMA_SIZE).trim() || PANORAMA_SIZE);
  const fullPrompt = PANORAMA_PREFIX + scene;

  try {
    const result = await asyncTextToImage({
      apiKey,
      model,
      prompt: fullPrompt,
      size,
      n: 1,
    });

    return {
      status: 200,
      json: {
        ...result,
        mode: "panorama_360",
        panorama_size: size,
        prompt_used: fullPrompt,
      },
    };
  } catch (e) {
    return { status: 502, json: { detail: String(e?.message || e) } };
  }
}
