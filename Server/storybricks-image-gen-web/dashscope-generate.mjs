/**
 * DashScope wan2.6-image 网关逻辑：
 * - 有 reference_images → 同步图像编辑（img2img / 多参考图）
 * - 无 reference_images → 异步文生图（轮询 task）
 */

const DASH_BASE = (process.env.DASHSCOPE_BASE_URL || "https://dashscope.aliyuncs.com/api/v1").replace(/\/$/, "");
const SYNC_URL = `${DASH_BASE}/services/aigc/multimodal-generation/generation`;
const ASYNC_URL = `${DASH_BASE}/services/aigc/image-generation/generation`;
const POLL_INTERVAL_MS = Number(process.env.POLL_INTERVAL_MS || 2000);
const POLL_MAX_ATTEMPTS = Number(process.env.POLL_MAX_ATTEMPTS || 90);

function getApiKey() {
  return (process.env.DASHSCOPE_API_KEY || "").trim();
}

function normalizeSize(size) {
  const s = String(size || "1920*1080").trim();
  if (!s) return "1920*1080";
  if (s === "1024*1024" || s === "1280*1280") return "1920*1080";
  if (s === "1K" || s === "2K") return s;
  return s;
}

function normalizeImageInput(raw) {
  const s = String(raw || "").trim();
  if (!s) return null;
  if (s.startsWith("http://") || s.startsWith("https://")) return s;
  if (s.startsWith("data:")) return s;
  // 裸 base64 → 默认 PNG
  return `data:image/png;base64,${s}`;
}

function buildMessages(prompt, referenceImages) {
  const content = [{ text: prompt }];
  for (const img of referenceImages) {
    const normalized = normalizeImageInput(img);
    if (normalized) content.push({ image: normalized });
  }
  return [{ role: "user", content }];
}

function extractImageUrls(payload) {
  const urls = [];
  const choices = payload?.output?.choices;
  if (!Array.isArray(choices)) return urls;

  for (const choice of choices) {
    const items = choice?.message?.content;
    if (!Array.isArray(items)) continue;
    for (const item of items) {
      const url = item?.image;
      if (url && typeof url === "string") urls.push(url);
    }
  }
  return urls;
}

function extractTaskId(payload) {
  return payload?.output?.task_id || payload?.task_id || null;
}

async function dashFetch(url, { apiKey, method = "POST", body, asyncMode = false }) {
  const headers = {
    Authorization: `Bearer ${apiKey}`,
    "Content-Type": "application/json",
  };
  if (asyncMode) headers["X-DashScope-Async"] = "enable";

  const res = await fetch(url, {
    method,
    headers,
    body: body != null ? JSON.stringify(body) : undefined,
  });

  const text = await res.text();
  let json;
  try {
    json = text ? JSON.parse(text) : {};
  } catch {
    throw new Error(`Invalid JSON from DashScope (${res.status}): ${text.slice(0, 400)}`);
  }

  if (!res.ok) {
    const msg = json.message || json.detail || json.error || text.slice(0, 400);
    throw new Error(`DashScope HTTP ${res.status}: ${msg}`);
  }
  if (json.code && json.code !== "" && json.code !== "Success") {
    throw new Error(`DashScope error ${json.code}: ${json.message || "unknown"}`);
  }
  return json;
}

async function syncImageEdit({ apiKey, model, prompt, referenceImages, size, n }) {
  const payload = {
    model,
    input: { messages: buildMessages(prompt, referenceImages) },
    parameters: {
      prompt_extend: true,
      watermark: false,
      n,
      enable_interleave: false,
      size,
    },
  };

  const json = await dashFetch(SYNC_URL, { apiKey, body: payload });
  const urls = extractImageUrls(json);
  if (urls.length === 0) {
    throw new Error("DashScope sync edit returned no image URL");
  }

  return {
    task_id: json.request_id || null,
    image_url: urls[0],
    image_urls: urls,
    model,
    mode: "image_edit",
    request_id: json.request_id || null,
  };
}

async function pollTask({ apiKey, taskId }) {
  for (let i = 0; i < POLL_MAX_ATTEMPTS; i++) {
    if (i > 0) await sleep(POLL_INTERVAL_MS);

    const json = await dashFetch(`${DASH_BASE}/tasks/${encodeURIComponent(taskId)}`, {
      apiKey,
      method: "GET",
    });

    const status = json?.output?.task_status;
    if (status === "SUCCEEDED") {
      const urls = extractImageUrls(json);
      if (urls.length === 0) throw new Error("Task succeeded but no image URL in response");
      return { json, urls };
    }
    if (status === "FAILED" || status === "CANCELED") {
      const msg = json?.output?.message || json?.message || status;
      throw new Error(`DashScope task ${status}: ${msg}`);
    }
  }
  throw new Error(`DashScope task timed out after ${POLL_MAX_ATTEMPTS} polls`);
}

async function asyncTextToImage({ apiKey, model, prompt, size, n }) {
  const payload = {
    model,
    input: {
      messages: buildMessages(prompt, []),
    },
    parameters: {
      prompt_extend: true,
      watermark: false,
      size,
      enable_interleave: true,
      max_images: n,
      n: 1,
    },
  };

  const created = await dashFetch(ASYNC_URL, { apiKey, body: payload, asyncMode: true });
  const taskId = extractTaskId(created);
  if (!taskId) throw new Error("DashScope async create returned no task_id");

  const { json, urls } = await pollTask({ apiKey, taskId });
  return {
    task_id: taskId,
    image_url: urls[0],
    image_urls: urls,
    model,
    mode: "text_to_image",
    request_id: json.request_id || null,
  };
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

export { getApiKey, asyncTextToImage, syncImageEdit };

/**
 * @param {Record<string, unknown>} body
 * @returns {Promise<{ status: number, json: Record<string, unknown> }>}
 */
export async function handleGenerate(body) {
  const apiKey = getApiKey();
  if (!apiKey) {
    return { status: 503, json: { detail: "DASHSCOPE_API_KEY not configured on server" } };
  }

  const prompt = String(body?.prompt || "").trim();
  if (!prompt) {
    return { status: 400, json: { detail: "prompt is required" } };
  }

  const model = String(body?.model || "wan2.6-image").trim() || "wan2.6-image";
  const size = normalizeSize(body?.size);
  const n = Math.min(Math.max(Number(body?.n) || 1, 1), 4);

  const refs = Array.isArray(body?.reference_images)
    ? body.reference_images.map((x) => String(x || "").trim()).filter(Boolean).slice(0, 4)
    : [];

  try {
    const result =
      refs.length > 0
        ? await syncImageEdit({ apiKey, model, prompt, referenceImages: refs, size, n })
        : await asyncTextToImage({ apiKey, model, prompt, size, n });

    return { status: 200, json: result };
  } catch (e) {
    return { status: 502, json: { detail: String(e?.message || e) } };
  }
}
