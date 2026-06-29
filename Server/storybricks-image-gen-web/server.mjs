/**
 * StoryBricks 生图网关
 *
 * POST /generate
 *   - prompt, model, size, n
 *   - reference_images?: string[]  (URL 或 data:image/...;base64,...，1~4 张 → img2img)
 *
 * 启动:
 *   cd Server/storybricks-image-gen-web
 *   set DASHSCOPE_API_KEY=sk-xxx
 *   node server.mjs
 */
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { handleGenerate } from "./dashscope-generate.mjs";
import { handleGeneratePanorama } from "./dashscope-panorama-generate.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const HOST = (process.env.HOST || "0.0.0.0").trim();
const PORT = Number(process.env.PORT || 8800);
const MAX_BODY_BYTES = Number(process.env.MAX_BODY_BYTES || 40 * 1024 * 1024);

loadDotEnv(path.join(__dirname, ".env"));

function loadDotEnv(filePath) {
  if (!fs.existsSync(filePath)) return;
  const text = fs.readFileSync(filePath, "utf8");
  for (const line of text.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eq = trimmed.indexOf("=");
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq).trim();
    let val = trimmed.slice(eq + 1).trim();
    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    // 本地 .env 优先于系统/终端里已有的同名变量（避免 Windows 用户环境变量覆盖项目 .env）
    process.env[key] = val;
  }
}

function dashKeySuffix() {
  const key = (process.env.DASHSCOPE_API_KEY || "").trim();
  return key.length >= 6 ? key.slice(-6) : "";
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let total = 0;
    req.on("data", (c) => {
      total += c.length;
      if (total > MAX_BODY_BYTES) {
        reject(new Error("body too large"));
        req.destroy();
        return;
      }
      chunks.push(c);
    });
    req.on("end", () => resolve(Buffer.concat(chunks)));
    req.on("error", reject);
  });
}

function sendJson(res, status, obj) {
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
  });
  res.end(JSON.stringify(obj));
}

const server = http.createServer(async (req, res) => {
  const host = req.headers.host || "127.0.0.1";
  const url = new URL(req.url || "/", `http://${host}`);

  if (req.method === "OPTIONS") {
    res.writeHead(204, {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
      "Access-Control-Max-Age": "86400",
    });
    res.end();
    return;
  }

  if (req.method === "GET" && url.pathname === "/health") {
    sendJson(res, 200, {
      ok: true,
      service: "storybricks-image-gen",
      has_api_key: !!(process.env.DASHSCOPE_API_KEY || "").trim(),
      dashscope_key_suffix: dashKeySuffix(),
      supports_reference_images: true,
      supports_panorama_360: true,
    });
    return;
  }

  if (req.method === "GET" && url.pathname === "/generate") {
    res.writeHead(302, { Location: "/" });
    res.end();
    return;
  }

  if (req.method === "POST" && url.pathname === "/generate") {
    let bodyBuf;
    try {
      bodyBuf = await readBody(req);
    } catch (e) {
      sendJson(res, 413, { detail: String(e.message || e) });
      return;
    }

    let body = {};
    try {
      body = bodyBuf.length ? JSON.parse(bodyBuf.toString("utf8")) : {};
    } catch {
      sendJson(res, 400, { detail: "Invalid JSON body" });
      return;
    }

    const { status, json } = await handleGenerate(body);
    sendJson(res, status, json);
    return;
  }

  if (req.method === "POST" && url.pathname === "/generate-panorama") {
    let bodyBuf;
    try {
      bodyBuf = await readBody(req);
    } catch (e) {
      sendJson(res, 413, { detail: String(e.message || e) });
      return;
    }

    let body = {};
    try {
      body = bodyBuf.length ? JSON.parse(bodyBuf.toString("utf8")) : {};
    } catch {
      sendJson(res, 400, { detail: "Invalid JSON body" });
      return;
    }

    const { status, json } = await handleGeneratePanorama(body);
    sendJson(res, status, json);
    return;
  }

  if (req.method === "GET" && (url.pathname === "/" || url.pathname === "/index.html")) {
    const htmlPath = path.join(__dirname, "index.html");
    if (fs.existsSync(htmlPath)) {
      res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
      res.end(fs.readFileSync(htmlPath, "utf8"));
      return;
    }
  }

  res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
  res.end("Not found");
});

server.listen(PORT, HOST, () => {
  console.log(`StoryBricks image-gen server: http://${HOST}:${PORT}/generate`);
  console.log(`Panorama 360: POST http://${HOST}:${PORT}/generate-panorama`);
  console.log(`Health: http://${HOST}:${PORT}/health`);
  const suffix = dashKeySuffix();
  if (suffix) {
    console.log(`DASHSCOPE_API_KEY: configured (…${suffix}, from .env)`);
  } else {
    console.warn("WARNING: DASHSCOPE_API_KEY not set — all /generate requests will fail.");
  }
});
