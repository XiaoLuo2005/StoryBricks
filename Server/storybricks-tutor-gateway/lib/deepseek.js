const {
  DEEPSEEK_API_KEY,
  DEEPSEEK_BASE_URL,
  DEEPSEEK_MODEL,
  hasDeepSeek,
} = require("./config");

async function deepseekChat(messages, { temperature = 0.6, max_tokens = 1200 } = {}) {
  if (!hasDeepSeek()) throw new Error("请配置 DEEPSEEK_API_KEY（见 .env.example）");

  const r = await fetch(`${DEEPSEEK_BASE_URL}/chat/completions`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${DEEPSEEK_API_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model: DEEPSEEK_MODEL,
      messages,
      temperature,
      max_tokens,
    }),
  });

  const text = await r.text();
  if (!r.ok) throw new Error(`DeepSeek HTTP ${r.status}: ${text.slice(0, 800)}`);

  const data = JSON.parse(text);
  const content = data?.choices?.[0]?.message?.content?.trim() || "";
  if (!content) throw new Error("DeepSeek 返回空内容");
  return content;
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

  return `你是 3～8 岁儿童故事创作的「语音小老师」，负责用口语提问。
规则：
- 根据故事、本页场景、前情、缺口，为每个缺口写一条 2～3 句的亲切中文提问，称呼「小朋友」，结尾邀请开口回答。
- CharacterBehavior：问这个角色在这页想做什么、在干什么。
- OptionalStoryElement：若给了参考话术，可略作口语化但保持原意。
- 结合前情举例，不要编造与场景矛盾的剧情。
- 只输出 JSON 数组，不要 markdown：[{"id":"gap_0","text":"提问内容"}]
故事：${storyTitle}
本页：${pageTitle}
场景：${scene || "（无）"}
前情：${previous || "（无前情）"}
缺口：
${gapBlock || "（无）"}`;
}

async function buildStoryCreationQuestions(body) {
  const gaps = Array.isArray(body.gaps) ? body.gaps : [];
  if (gaps.length === 0) return [];

  const raw = await deepseekChat(
    [
      { role: "system", content: buildStoryCreationQuestionPrompt(body) },
      { role: "user", content: "请为每个缺口各生成一条儿童化语音提问。" },
    ],
    { temperature: 0.65, max_tokens: 1200 },
  );

  const jsonStart = raw.indexOf("[");
  const jsonEnd = raw.lastIndexOf("]");
  const slice = jsonStart >= 0 && jsonEnd > jsonStart ? raw.slice(jsonStart, jsonEnd + 1) : raw;
  let parsed;
  try {
    parsed = JSON.parse(slice);
  } catch {
    throw new Error(`DeepSeek 提问 JSON 无效: ${raw.slice(0, 400)}`);
  }
  if (!Array.isArray(parsed)) throw new Error("DeepSeek 提问结果不是数组");

  return parsed
    .filter((q) => q && String(q.text || "").trim())
    .map((q, i) => ({
      id: String(q.id || `gap_${i}`).trim(),
      text: String(q.text).trim(),
    }));
}

function buildStoryCreationImagePromptMessages(body) {
  const storyTitle = String(body.storyTitle || "故事").trim();
  const pageTitle = String(body.pageTitle || "").trim();
  const style = String(body.stylePromptPrefix || "").trim();
  const scene = String(body.sceneGuideText || "").trim();
  const previous = String(body.previousSummary || "").trim();
  const voice = String(body.voiceSupplement || "").trim();
  const roles = String(body.detectedRolesDescription || "").trim();
  const refClause = String(body.referenceImageClause || "").trim();
  const isContinuation = Boolean(body.isContinuationPage);

  const system = `你是儿童绘本 img2img 提示词专家。任务：把分散的输入整理成一段连贯、具体、可画的中文场景描述，供图像模型生成单页插画。

规则：
- 只输出一段中文正文，不要标题、不要 markdown、不要 JSON、不要解释。
- 长度约 120～280 字，信息密度高，画面感强。
- 必须融合：画风要求、本页场景、前情延续、孩子补充的角色行为与道具，写成统一叙事，禁止「前情：」「儿童语音补充：」等标签式罗列。
- 明确写出出场角色及其动作、站位、表情；环境背景要具体（地点、时间感、关键物体）。
- 已有参考图锁定角色外貌，描述中写角色名即可。
- 禁止输出「参考图1」「图1的…外貌」「生成儿童绘本插画」等 img2img 套话（程序会自动加在句首）。
- 横版 16:9 构图；角色不要特写大头；留出天空与背景。
- 禁止画面内出现任何文字、对话框、字幕、水印。
- 若 isContinuationPage 为 true：必须与前面页面不同的新场景、新构图，勿复述上一页布局。`;

  const user = [
    `故事：${storyTitle}`,
    `本页：${pageTitle}`,
    style && `画风前缀：${style}`,
    scene && `本页情境：${scene}`,
    previous && `前情摘要：${previous}`,
    voice && `孩子补充：${voice}`,
    roles && `本页识别到的角色：${roles}`,
    refClause && `（程序句首会自动附加：${refClause}）`,
    `是否续页：${isContinuation ? "是，需新场景" : "否，首页"}`,
    "",
    "请输出整理后的场景描述正文：",
  ]
    .filter(Boolean)
    .join("\n");

  return [
    { role: "system", content: system },
    { role: "user", content: user },
  ];
}

async function buildStoryCreationImagePrompt(body) {
  const content = await deepseekChat(buildStoryCreationImagePromptMessages(body), {
    temperature: 0.45,
    max_tokens: 600,
  });
  return content.replace(/^["'「]|["'」]$/g, "").trim();
}

async function refineChildAnswer(transcript, fields) {
  const question = String(fields.question || "").trim();
  const storyTitle = String(fields.storyTitle || "").trim();
  const pageTitle = String(fields.pageTitle || "").trim();
  const scene = String(fields.sceneGuideText || "").trim();

  const system = `你是儿童故事创作助手。把 3～8 岁孩子的口语回答整理成一句简短、通顺的中文，保留原意与童趣，不要添加新剧情。只输出整理后的一句话，不要解释。`;
  const user = [
    storyTitle && `故事：${storyTitle}`,
    pageTitle && `本页：${pageTitle}`,
    scene && `场景：${scene}`,
    question && `提问：${question}`,
    `孩子原话：${transcript}`,
  ]
    .filter(Boolean)
    .join("\n");

  return deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user },
    ],
    { temperature: 0.3, max_tokens: 256 },
  );
}

/** 故事创作 ASR：不接收「提问」全文，避免把问题里的举例（旗帜、鲜花等）写进孩子回答。 */
async function refineStoryCreationAnswer(transcript, fields = {}) {
  const roleName = String(fields.roleName || "").trim();
  const gapKind = String(fields.gapKind || "").trim();

  const system = `你是儿童故事创作助手。仅把孩子语音识别原文整理成一句简短、通顺的中文。
规则：
- 严格保留孩子原意；禁止添加原话中没有的物品、角色、动作或情节。
- 禁止参考或复述任何「提问」「举例」「比如」里的内容。
- 若原话表示不要、没有、跳过，输出「不用加」或原话核心意思。
- 只做口语顺句，不扩写。只输出一句话，不要解释。`;

  const user = [
    gapKind && `回答类型：${gapKind}`,
    roleName && `相关角色：${roleName}`,
    `孩子原话：${transcript}`,
  ]
    .filter(Boolean)
    .join("\n");

  return deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user },
    ],
    { temperature: 0.2, max_tokens: 180 },
  );
}

module.exports = {
  deepseekChat,
  buildStoryCreationQuestions,
  buildStoryCreationImagePrompt,
  buildStoryCreationImagePromptMessages,
  refineChildAnswer,
  refineStoryCreationAnswer,
  hasDeepSeek,
};
