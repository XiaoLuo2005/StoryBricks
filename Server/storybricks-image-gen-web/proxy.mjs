/**
 * 本地静态页 + 将 POST /generate 转发到上游（避免浏览器直连公网 API 时的 CORS 问题）。
 *
 * 用法（需 Node 18+，自带 fetch）:
 *   cd Server/storybricks-image-gen-web
 *   set STORYBRICKS_GENERATE_URL=http://39.97.174.49:8800/generate
 *   node proxy.mjs
 *
 * 浏览器打开: http://127.0.0.1:8765/
 */
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = Number(process.env.PORT || 8765);
const DEFAULT_UPSTREAM = "http://39.97.174.49:8800/generate";
const UPSTREAM = (process.env.STORYBRICKS_GENERATE_URL || DEFAULT_UPSTREAM).trim();

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => resolve(Buffer.concat(chunks)));
    req.on("error", reject);
  });
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

  // 浏览器地址栏打开 /generate 会发 GET，生图接口只接受 POST → 上游会返回 405；引导回首页
  if (req.method === "GET" && url.pathname === "/generate") {
    res.writeHead(302, { Location: "/" });
    res.end();
    return;
  }

  if (req.method === "POST" && url.pathname === "/generate") {
    const bodyBuf = await readBody(req);
    try {
      const r = await fetch(UPSTREAM, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: bodyBuf.length ? bodyBuf : undefined,
      });
      const buf = Buffer.from(await r.arrayBuffer());
      res.writeHead(r.status, {
        "Content-Type": r.headers.get("content-type") || "application/json; charset=utf-8",
        "Access-Control-Allow-Origin": "*",
      });
      res.end(buf);
    } catch (e) {
      res.writeHead(502, {
        "Content-Type": "application/json; charset=utf-8",
        "Access-Control-Allow-Origin": "*",
      });
      res.end(JSON.stringify({ error: String(e && e.message ? e.message : e) }));
    }
    return;
  }

  if (req.method === "GET" && (url.pathname === "/" || url.pathname === "/index.html")) {
    const html = fs.readFileSync(path.join(__dirname, "index.html"), "utf8");
    res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
    res.end(html);
    return;
  }

  res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
  res.end("Not found");
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`StoryBricks 生图页: http://127.0.0.1:${PORT}/`);
  console.log(`转发目标: ${UPSTREAM}`);
});
