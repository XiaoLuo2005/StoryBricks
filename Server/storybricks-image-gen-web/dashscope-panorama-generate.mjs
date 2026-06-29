/**
 * 360 全景图（equirectangular 2:1）：
 * - 有 source_image / reference_images → 图生图（把绘本页扩展成环视全景）
 * - 无参考图 → 文生图
 */

import { asyncTextToImage, getApiKey, syncImageEdit } from "./dashscope-generate.mjs";

const PANORAMA_SIZE = (process.env.PANORAMA_SIZE || "1536*768").trim();
const PANORAMA_MODEL = (process.env.PANORAMA_MODEL || process.env.PANORAMA_IMAGE_MODEL || "wan2.6-image").trim();
const PANORAMA_MIN_PIXELS = 589_824;
/** enable_interleave=true（文生单图）时 wan2.6-image 上限约 1280×1280 */
const PANORAMA_MAX_PIXELS = 1_638_400;

const PANORAMA_TEXT_PREFIX =
  "360 degree equirectangular panorama, seamless wrap-around, immersive environment, " +
  "no black borders, no text, no watermark, 2:1 aspect ratio, ";

const PANORAMA_IMG2IMG_PREFIX =
  "将这张儿童绘本插画扩展为无缝360度等距圆柱全景图（equirectangular 2:1）。" +
  "保持原有角色、画风、色彩与构图中心内容一致，向四周自然延伸天空、地面与环境；" +
  "角色必须保留且清晰可见，不要删除任何角色；无黑边、无文字、无水印；" +
  "禁止空白对话框、对白气泡、漫画台词框或任何留白文字区域。";

function parseSizePair(size) {
  const m = String(size || "")
    .trim()
    .match(/^(\d+)\s*[*xX×]\s*(\d+)$/);
  if (!m) return null;
  return { width: Number(m[1]), height: Number(m[2]) };
}

/** 文生全景：总像素 ∈ [589824, 1638400]；默认 1536×768。 */
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

function collectSourceImages(body) {
  const fromSource = String(body?.source_image || "").trim();
  if (fromSource) return [fromSource];

  if (!Array.isArray(body?.reference_images)) return [];
  return body.reference_images.map((x) => String(x || "").trim()).filter(Boolean).slice(0, 1);
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
  const sourceImages = collectSourceImages(body);
  const hasSource = sourceImages.length > 0;

  if (!scene && !hasSource) {
    return { status: 400, json: { detail: "prompt or source_image is required" } };
  }

  const model = String(body?.model || PANORAMA_MODEL).trim() || PANORAMA_MODEL;
  const size = normalizePanoramaSize(String(body?.size || PANORAMA_SIZE).trim() || PANORAMA_SIZE);

  const fullPrompt = hasSource
    ? `${PANORAMA_IMG2IMG_PREFIX}${scene ? ` 场景补充：${scene}` : ""}`
    : `${PANORAMA_TEXT_PREFIX}${scene}`;

  try {
    const result = hasSource
      ? await syncImageEdit({
          apiKey,
          model,
          prompt: fullPrompt,
          referenceImages: sourceImages,
          size,
          n: 1,
        })
      : await asyncTextToImage({
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
        mode: hasSource ? "panorama_360_img2img" : "panorama_360",
        panorama_size: size,
        prompt_used: fullPrompt,
      },
    };
  } catch (e) {
    return { status: 502, json: { detail: String(e?.message || e) } };
  }
}
