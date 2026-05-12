/**
 * 读取目录下所有 PNG（按文件名中的数字排序），调用 DashScope 视觉模型，
 * 生成与 StoryBricks 教程助教兼容的三份 txt：
 *   {prefix}TutorOverview.txt
 *   {prefix}StepHints.txt
 *   {prefix}StepDetails.txt
 *
 * 依赖：Node 18+，环境变量 DASHSCOPE_API_KEY（与网关相同）
 *
 * 用法：
 *   node tools/generate-tutorial-tutor-texts.mjs "G:/path/to/Assets/Step/Rabbit"
 *   node tools/generate-tutorial-tutor-texts.mjs ./path --prefix RabbitTutorial --model qwen-vl-plus --theme=兔子
 */
import fs from "fs";
import path from "path";
import process from "process";

const DASH_KEY = process.env.DASHSCOPE_API_KEY || "";
const DASH_COMPAT = (process.env.DASHSCOPE_COMPAT_BASE || "https://dashscope.aliyuncs.com/compatible-mode/v1").replace(/\/$/, "");
const VL_MODEL = process.env.DASHSCOPE_VL_MODEL || "qwen-vl-plus";

function parseArgs(argv) {
  const out = { dir: "", prefix: "", model: VL_MODEL, themeZh: "", dryRun: false };
  for (const a of argv) {
    if (a === "--dry-run") out.dryRun = true;
    else if (a.startsWith("--prefix=")) out.prefix = a.slice("--prefix=".length).trim();
    else if (a.startsWith("--model=")) out.model = a.slice("--model=".length).trim();
    else if (a.startsWith("--theme=")) out.themeZh = a.slice("--theme=".length).trim();
    else if (!a.startsWith("-") && !out.dir) out.dir = a;
  }
  return out;
}

/**
 * 常见目录英文名 -> 中文主题（可继续扩充）。
 * 未命中时由模型结合图片与目录名自行推断。
 */
const FOLDER_THEME_HINTS = {
  rabbit: "兔子",
  bunny: "兔子",
  cat: "小猫",
  dog: "小狗",
  duck: "鸭子",
  car: "小车",
  house: "小房子",
  robot: "机器人",
  dinosaur: "恐龙",
  dragon: "龙",
  plane: "飞机",
  ship: "轮船",
  train: "火车",
};

/** 把文件夹名变成给 VL 的「拼什么」语义说明；themeZhOverride 来自 --theme= 时优先生效 */
function folderSemanticPrompt(dirAbs, themeZhOverride) {
  const raw = path.basename(dirAbs).trim();
  if (!raw) return "";
  if (themeZhOverride && String(themeZhOverride).trim()) {
    const t = String(themeZhOverride).trim();
    return (
      `【文件夹名 / 主题】\n` +
        `当前目录名为「${raw}」。用户指定本套造型中文主题为「${t}」，请全文统一使用该称呼；总览开头一两句点明在拼什么。\n\n`
    );
  }
  const key = raw
    .toLowerCase()
    .normalize("NFKD")
    .replace(/\s+/g, "")
    .replace(/[_-]/g, "");
  const alpha = key.replace(/[^a-z0-9]/g, "");
  const mapped = alpha && FOLDER_THEME_HINTS[alpha] ? FOLDER_THEME_HINTS[alpha] : "";
  const hasHan = /[\u4e00-\u9fff]/.test(raw);
  const lines = [
    `【文件夹名 / 主题线索】`,
    `当前图片所在**最后一级文件夹名**为「${raw}」。这是独立语义线索，表示本套积木在拼什么（造型/主题），请与图片内容交叉印证后采用。`,
  ];
  if (mapped) {
    lines.push(
      `由目录名约定中文主题为「${mapped}」。请在 tutorialTutorOverview 首段点明；stepHints、stepDetails 全文统一用该称呼，不要擅自改成其它造型名。`
    );
  } else if (hasHan) {
    lines.push(`目录名含中文，请将「${raw}」视为教程主题名写入总览，并在全文保持称呼一致。`);
  } else {
    lines.push(
      `目录名为字母/数字代号类。请结合图片与常识推断儿童易懂的中文主题名（例如 Rabbit→兔子），写入总览首段并全文统一；若无法可靠推断，可用「本模型」指代，不要编造具体动物或载具名。`
    );
  }
  lines.push(`tutorialTutorOverview 开头一两句必须交代「在拼什么」。`);
  return lines.join("\n") + "\n\n";
}

function defaultPrefix(dirAbs) {
  const base = path.basename(dirAbs);
  if (!base) return "Tutorial";
  return base.charAt(0).toUpperCase() + base.slice(1) + "Tutorial";
}

function stepIndexFromName(file) {
  const m = path.basename(file, path.extname(file)).match(/(\d+)/);
  return m ? parseInt(m[1], 10) : 0;
}

function listSortedPngs(dirAbs) {
  const names = fs.readdirSync(dirAbs).filter((n) => /\.png$/i.test(n));
  const full = names.map((n) => path.join(dirAbs, n));
  full.sort((a, b) => {
    const da = stepIndexFromName(a);
    const db = stepIndexFromName(b);
    if (da !== db) return da - db;
    return path.basename(a).localeCompare(path.basename(b));
  });
  return full;
}

function extractJsonObject(text) {
  const t = String(text || "").trim();
  const fence = /^```(?:json)?\s*([\s\S]*?)```$/m.exec(t);
  const body = fence ? fence[1].trim() : t;
  const start = body.indexOf("{");
  const end = body.lastIndexOf("}");
  if (start === -1 || end <= start) throw new Error("响应中未找到 JSON 对象");
  return JSON.parse(body.slice(start, end + 1));
}

function buildDetailsTxt(detailsArr) {
  const header = [
    "# 本文件由 tools/generate-tutorial-tutor-texts.mjs 生成；可手改。",
    "# 分步：每个新步骤前单独一行写三个英文冒号、STEP、再三个英文冒号（与下一行相同）。",
    "# 不要在注释正文中连续写该分隔符，以免解析误切。",
    "",
  ].join("\n");
  const blocks = detailsArr.map((d) => {
    const g = String(d.stepGoal || "").trim();
    const p = String(d.partsUsed || "").trim();
    const k = String(d.keyActions || "").trim();
    const f = String(d.pitfalls || "").trim();
    return [
      ":::STEP:::",
      `GOAL: ${g}`,
      `PARTS: ${p}`,
      `ACTIONS: ${k}`,
      `PITFALLS: ${f}`,
    ].join("\n");
  });
  return header + blocks.join("\n") + "\n";
}

function buildHintsTxt(hintsArr) {
  const lines = hintsArr.map((h) => String(h || "").trim().replace(/\r?\n/g, " "));
  return [
    "# 每行对应一步；# 开头为注释。",
    "",
    ...lines,
    "",
  ].join("\n");
}

function buildOverviewTxt(s) {
  const t = String(s || "").trim();
  return [
    "# 本文件由 tools/generate-tutorial-tutor-texts.mjs 生成；可手改。",
    "",
    t,
    "",
  ].join("\n");
}

let _resizeNoteLogged = false;

/** 若已安装 sharp，将 PNG 缩放到长边不超过 TUTOR_GEN_MAX_EDGE（默认 1200），避免多图 base64 撑爆上下文。 */
async function loadPngBuffer(filePath) {
  const raw = fs.readFileSync(filePath);
  try {
    const { default: sharp } = await import("sharp");
    const edge = Number(process.env.TUTOR_GEN_MAX_EDGE || 1200);
    const out = await sharp(raw)
      .resize({ width: edge, height: edge, fit: "inside", withoutEnlargement: true })
      .png({ compressionLevel: 9 })
      .toBuffer();
    if (!_resizeNoteLogged) {
      console.log("已用 sharp 缩放图片（长边≤", edge, "px），以减小 DashScope 请求体积。");
      _resizeNoteLogged = true;
    }
    return out;
  } catch {
    if (!_resizeNoteLogged) {
      console.warn(
        "未使用 sharp，将发送原图。多张大图可能导致 API 失败。可在本目录执行: npm install sharp"
      );
      _resizeNoteLogged = true;
    }
    return raw;
  }
}

async function dashChat(model, messages, maxTokens = 8192) {
  const url = `${DASH_COMPAT}/chat/completions`;
  const r = await fetch(url, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${DASH_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model,
      messages,
      temperature: 0.2,
      max_tokens: maxTokens,
    }),
  });
  const raw = await r.text();
  if (!r.ok) throw new Error(`VL HTTP ${r.status}: ${raw.slice(0, 2000)}`);
  const data = JSON.parse(raw);
  const content = data?.choices?.[0]?.message?.content;
  if (!content) throw new Error(`VL 无正文: ${raw.slice(0, 800)}`);
  return content;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  let dirAbs = args.dir ? path.resolve(args.dir) : "";
  if (!dirAbs) {
    console.error(
      "用法: node tools/generate-tutorial-tutor-texts.mjs <含步骤PNG的目录> [--prefix=RabbitTutorial] [--model=qwen-vl-plus] [--theme=兔子] [--dry-run]\n" +
        "环境变量: DASHSCOPE_API_KEY（必填）, DASHSCOPE_COMPAT_BASE（可选）, DASHSCOPE_VL_MODEL（可选，默认 qwen-vl-plus）\n" +
        "说明: 会读取**最后一级文件夹名**作为造型语义（如 Rabbit→兔子）；也可用 --theme= 强制指定中文主题。"
    );
    process.exit(1);
  }
  if (!DASH_KEY) {
    console.error("请设置环境变量 DASHSCOPE_API_KEY");
    process.exit(1);
  }
  if (!fs.existsSync(dirAbs) || !fs.statSync(dirAbs).isDirectory()) {
    console.error("目录不存在或不是文件夹:", dirAbs);
    process.exit(1);
  }

  const pngs = listSortedPngs(dirAbs);
  if (pngs.length === 0) {
    console.error("目录下没有 .png 文件:", dirAbs);
    process.exit(1);
  }

  const prefix = args.prefix || defaultPrefix(dirAbs);
  const model = args.model || VL_MODEL;
  const stepCount = pngs.length;

  const folderBlock = folderSemanticPrompt(dirAbs, args.themeZh);

  const contentParts = [];
  contentParts.push({
    type: "text",
    text:
      folderBlock +
        `下面共 ${stepCount} 张图，按顺序为第 1 步到第 ${stepCount} 步的积木拼装说明书截图（含零件表小框与主视图）。\n` +
        `请只依据图片内容，不要臆造图中没有的零件编号。\n` +
        `输出**仅一个 JSON 对象**（不要 Markdown 代码围栏），字段如下：\n` +
        `{\n` +
        `  "tutorialTutorOverview": "字符串：整套教程总览（套装感、前后方向约定、安全提示、给助教的规则），用中文，可多段。",\n` +
        `  "stepHints": ["第1步一句短提示", "第2步...", ...] 长度必须=${stepCount},\n` +
        `  "stepDetails": [\n` +
        `    {"stepGoal":"","partsUsed":"","keyActions":"","pitfalls":""},\n` +
        `    ... 共 ${stepCount} 个对象，顺序与图片一致\n` +
        `  ]\n` +
        `}\n` +
        `stepDetails 每项对应一步：stepGoal=本步目标；partsUsed=本步零件（颜色/形状/数量）；keyActions=关键动作与方向；pitfalls=易错点。`,
  });

  let totalBytes = 0;
  for (let i = 0; i < pngs.length; i++) {
    const buf = await loadPngBuffer(pngs[i]);
    totalBytes += buf.length;
    const b64 = buf.toString("base64");
    const uri = `data:image/png;base64,${b64}`;
    contentParts.push({
      type: "text",
      text: `\n—— 第 ${i + 1} 步 图片文件名: ${path.basename(pngs[i])} ——\n`,
    });
    contentParts.push({
      type: "image_url",
      image_url: { url: uri },
    });
  }

  if (args.dryRun) {
    const base = path.basename(dirAbs);
    const alphaKey = base
      .toLowerCase()
      .normalize("NFKD")
      .replace(/\s+/g, "")
      .replace(/[_-]/g, "")
      .replace(/[^a-z0-9]/g, "");
    const mapped = alphaKey ? FOLDER_THEME_HINTS[alphaKey] || "" : "";
    const hintLine =
      args.themeZh ||
      mapped ||
      (/[\u4e00-\u9fff]/.test(base) ? base : "(目录名无常见英文映射，由模型结合图推断)");
    const approx = contentParts.reduce((n, p) => {
      if (p.type === "text") return n + p.text.length;
      if (p.type === "image_url") return n + (p.image_url?.url?.length || 0);
      return n;
    }, 0);
    console.log("dry-run: 目录名=", base, "主题线索=", hintLine);
    console.log(
      "dry-run: 图片张数=",
      stepCount,
      "解码后约",
      totalBytes,
      "字节；base64+文本近似字符量=",
      approx,
      "model=",
      model
    );
    process.exit(0);
  }

  console.log("调用视觉模型", model, "… 图片:", stepCount, "张；目录:", path.basename(dirAbs), args.themeZh ? `；主题=${args.themeZh}` : "");
  const rawContent = await dashChat(model, [{ role: "user", content: contentParts }], 8192);
  let data;
  try {
    data = extractJsonObject(rawContent);
  } catch (e) {
    console.error("JSON 解析失败:", e.message);
    console.error("模型原文前 4000 字:\n", rawContent.slice(0, 4000));
    process.exit(1);
  }

  const overview = data.tutorialTutorOverview ?? "";
  const hints = Array.isArray(data.stepHints) ? data.stepHints : [];
  const details = Array.isArray(data.stepDetails) ? data.stepDetails : [];

  if (hints.length !== stepCount) {
    console.warn(`警告: stepHints 长度 ${hints.length}，期望 ${stepCount}，将截断或补空行。`);
  }
  if (details.length !== stepCount) {
    console.warn(`警告: stepDetails 长度 ${details.length}，期望 ${stepCount}，将截断或补空块。`);
  }

  const hintsNorm = Array.from({ length: stepCount }, (_, i) => String(hints[i] || "").trim());
  const detailsNorm = Array.from({ length: stepCount }, (_, i) => {
    const d = details[i] || {};
    return {
      stepGoal: String(d.stepGoal ?? "").trim(),
      partsUsed: String(d.partsUsed ?? "").trim(),
      keyActions: String(d.keyActions ?? "").trim(),
      pitfalls: String(d.pitfalls ?? "").trim(),
    };
  });

  const outOverview = path.join(dirAbs, `${prefix}TutorOverview.txt`);
  const outHints = path.join(dirAbs, `${prefix}StepHints.txt`);
  const outDetails = path.join(dirAbs, `${prefix}StepDetails.txt`);

  fs.writeFileSync(outOverview, buildOverviewTxt(overview), "utf8");
  fs.writeFileSync(outHints, buildHintsTxt(hintsNorm), "utf8");
  fs.writeFileSync(outDetails, buildDetailsTxt(detailsNorm), "utf8");

  console.log("已写入:");
  console.log(" ", outOverview);
  console.log(" ", outHints);
  console.log(" ", outDetails);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
