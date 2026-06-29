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

  return `你是 3～8 岁儿童故事创作的语音助手「乐乐」，负责用口语提问。
规则：
- 根据故事、本页场景、前情、缺口，为每个缺口写一条 2～3 句的亲切中文提问，自称「乐乐」，称呼「小朋友」，直接邀请孩子开口回答（不要说「你好乐乐」唤醒词）。
- CharacterBehavior：问这个角色在这页想做什么、在干什么。
- CharacterPosition：问多个角色谁在前谁在后、离场景元素远近，或要不要调整站位。
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
  const mandatory = String(body.mandatoryRolesClause || "").trim();
  const refClause = String(body.referenceImageClause || "").trim();
  const isContinuation = Boolean(body.isContinuationPage);

  const system = `你是儿童绘本 img2img 提示词专家。任务：把分散的输入整理成一段连贯、具体、可画的中文场景描述，供图像模型生成单页插画。

规则：
- 只输出一段中文正文，不要标题、不要 markdown、不要 JSON、不要解释。
- 长度约 120～280 字，信息密度高，画面感强。
- 必须融合：画风要求、本页场景、前情延续、孩子补充的角色行为与道具，写成统一叙事，禁止「前情：」「儿童语音补充：」等标签式罗列。
- 明确写出出场角色及其动作、站位、表情；环境背景要具体（地点、时间感、关键物体）。
- 若提供了 mandatoryRolesClause，必须严格遵守：镜头里已识别的每个角色都要画进画面，不得只画对话里提到的角色。
- 已有参考图锁定角色外貌，描述中写角色名即可。
- 禁止输出「参考图1」「图1的…外貌」「生成儿童绘本插画」等 img2img 套话（程序会自动加在句首）。
- 横版 16:9 构图；角色不要特写大头；留出天空与背景。
- 禁止画面内出现任何文字、对话框、字幕、水印；故事文字由程序在固定 UI 区域展示，插画里不要写字。
- 若 isContinuationPage 为 true：必须与前面页面不同的新场景、新构图，勿复述上一页布局。`;

  const user = [
    `故事：${storyTitle}`,
    `本页：${pageTitle}`,
    style && `画风前缀：${style}`,
    scene && `本页情境：${scene}`,
    previous && `前情摘要：${previous}`,
    voice && `孩子补充：${voice}`,
    roles && `本页识别到的角色：${roles}`,
    mandatory && `角色硬性要求：${mandatory}`,
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
function parseJsonObject(raw, label) {
  const text = String(raw || "").trim();
  const start = text.indexOf("{");
  const end = text.lastIndexOf("}");
  const slice = start >= 0 && end > start ? text.slice(start, end + 1) : text;
  try {
    return JSON.parse(slice);
  } catch {
    throw new Error(`${label} JSON 无效: ${text.slice(0, 400)}`);
  }
}

async function buildStoryCreationReply(body) {
  const system = `你是 3～8 岁儿童故事共创语音助手「乐乐」。孩子正在通过对话补全「本页故事缺口」，你要像有耐心的故事伙伴，不是机械问卷。

## 缺口类型 gapKind
- CharacterBehavior：某角色的动作/在做什么
- CharacterPosition：角色之间的位置、谁在前谁在后
- OptionalStoryElement：本页可选道具/场景元素

## 意图 intent（必填，小写英文）
- answered：回答与当前缺口相关，信息可用
- incomplete：沾边但太短/太模糊，还需追问
- repeat_question：没听清、要求重复（如「什么」「再说一遍」「没听见」）
- clarify：不懂问题意思（如「什么意思」「听不懂」）
- off_topic：跑题、闲聊、与当前故事缺口无关

## 输出规则
- acknowledgement：1～2 句口语，温暖；repeat/clarify 先安抚；off_topic 温柔拉回，不批评
- followUpQuestion：还需孩子回答时的下一句。repeat/clarify 用更短更易懂的话重问同一缺口；incomplete 只追问 1 个小点；off_topic 先简短回应再回扣主题；answered 且信息已够则留空
- extractedAnswer：仅写入本句里与缺口相关的「故事事实」，不要把「什么」「不知道」「再说一遍」写进去；跑题则空
- conversationDone：仅当 intent=answered 且 extractedAnswer 已足够回答缺口；或 turnIndex>=5 时可为 true 并尽量保留已有 extractedAnswer
- 同一缺口最多 6 轮（turnIndex 0～5）

只输出 JSON：
{"intent":"answered|incomplete|repeat_question|clarify|off_topic","acknowledgement":"…","followUpQuestion":"…或空","extractedAnswer":"…或空","conversationDone":true/false}`;

  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `本页：${body.pageTitle}`,
    body.sceneGuideText && `场景：${body.sceneGuideText}`,
    body.roleName && `角色：${body.roleName}`,
    body.gapKind && `缺口类型：${body.gapKind}`,
    body.originalQuestion && `最初提问：${body.originalQuestion}`,
    body.question && `当前提问：${body.question}`,
    body.answer && `孩子刚说：${body.answer}`,
    body.turnIndex != null && `当前轮次 turnIndex：${body.turnIndex}`,
    body.gapConversationLog && `本缺口对话记录：\n${body.gapConversationLog}`,
    body.previousSummary && `前情：${body.previousSummary}`,
  ]
    .filter(Boolean)
    .join("\n");

  const raw = await deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "请生成回应 JSON" },
    ],
    { temperature: 0.65, max_tokens: 520 },
  );
  const obj = parseJsonObject(raw, "reply");
  const intent = normalizeReplyIntent(obj.intent);
  const followUp = String(obj.followUpQuestion || "").trim();
  const extracted = String(obj.extractedAnswer || "").trim();
  let done = Boolean(obj.conversationDone);
  if (intent === "repeat_question" || intent === "clarify" || intent === "off_topic") {
    done = false;
  } else if (intent === "answered" && extracted) {
    done = done || !followUp;
  } else if (intent === "incomplete") {
    done = false;
  }
  if (Number(body.turnIndex) >= 5) {
    done = true;
  }
  return {
    intent,
    acknowledgement: String(obj.acknowledgement || defaultAck(intent)).trim(),
    followUpQuestion: followUp,
    extractedAnswer: extracted,
    conversationDone: done,
  };
}

function normalizeReplyIntent(raw) {
  const s = String(raw || "answered").trim().toLowerCase();
  if (s.includes("repeat")) return "repeat_question";
  if (s.includes("clarify")) return "clarify";
  if (s.includes("off") || s.includes("topic")) return "off_topic";
  if (s.includes("incomplete")) return "incomplete";
  return "answered";
}

function defaultAck(intent) {
  switch (intent) {
    case "repeat_question":
      return "好呀，乐乐再说一遍！";
    case "clarify":
      return "没关系，乐乐换个说法问你！";
    case "off_topic":
      return "哈哈，我们先把这个故事说完好不好？";
    case "incomplete":
      return "嗯嗯，再说一点点乐乐就懂啦！";
    default:
      return "好的，我记住啦！";
  }
}

async function buildStoryCreationPageCaption(body) {
  const maxChars = Math.min(Math.max(Number(body.maxChars) || 120, 40), 200);
  const system = `你是 3～8 岁儿童绘本的「讲稿作者」。请根据本页情节与孩子对话，写一段固定展示在画面右下角的绘本旁白。
规则：
- 语气温暖、像老师在给孩子讲故事；句子短，口语化，适合朗读。
- 可自然融入角色简短对话（如：兔子说：「我再睡一会儿。」），但不要写成剧本格式。
- 必须与前情、本页场景一致，保留孩子共创的内容。
- 若对话只提到部分角色，旁白仍要覆盖镜头里全部角色（可简短交代未发言角色的状态）。
- 全角汉字与标点合计不超过 ${maxChars} 字；超出则自行删减。
- 禁止出现「本页」「小朋友」「AI」等元叙述；禁止标题、编号、markdown。
- 只输出一段正文。`;

  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `本页：${body.pageTitle}`,
    body.sceneGuideText && `场景：${body.sceneGuideText}`,
    body.previousSummary && `前情：${body.previousSummary}`,
    body.pageSummary && `本页情节摘要：${body.pageSummary}`,
    body.conversationLog && `本页对话：\n${body.conversationLog}`,
  ]
    .filter(Boolean)
    .join("\n\n");

  const raw = await deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "请写本页绘本旁白" },
    ],
    { temperature: 0.55, max_tokens: 280 },
  );

  return clampCaptionText(raw, maxChars);
}

function clampCaptionText(text, maxChars) {
  let t = String(text || "")
    .trim()
    .replace(/\r\n/g, " ")
    .replace(/\n/g, " ");
  while (t.includes("  ")) t = t.replace("  ", " ");
  if (t.length <= maxChars) return t;
  if (maxChars <= 1) return "…";
  return `${t.slice(0, maxChars - 1).replace(/[，。、 ]$/u, "")}…`;
}

async function buildStoryCreationPageSummary(body) {
  const system = `你是儿童绘本共创助手「乐乐」。请把本页所有对话整理成一段 2～4 句的中文故事描述，供确认与生图使用。
规则：第三人称、画面感、保留孩子原意；只输出正文，不要标题或 JSON。`;
  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `本页：${body.pageTitle}`,
    body.sceneGuideText && `场景：${body.sceneGuideText}`,
    body.previousSummary && `前情：${body.previousSummary}`,
    body.conversationLog && `本页对话：\n${body.conversationLog}`,
  ]
    .filter(Boolean)
    .join("\n\n");

  return deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "请输出本页故事摘要" },
    ],
    { temperature: 0.5, max_tokens: 320 },
  );
}

async function buildStoryCreationFreeChat(body) {
  const system = `你是 3～8 岁儿童故事创作页的语音伙伴「乐乐」，孩子正在摆实体积木、看摄像头画面。
规则：
- 句子短（2～4 句），耐心鼓励，像陪玩姐姐/哥哥，不是考官。
- 孩子边摆边说时：先接话、重复你听到的关键词，再轻轻问一个开放小问（可选），不要按「第几题」形式提问。
- 若孩子没听清、问「什么」「再说一遍」，简短解释或重复你上一句要点。
- 若跑题（游戏、吃饭、无关闲聊），温柔回应一句再拉回本页故事或摆放。
- 可提示缺谁、怎么摆，但不要代替孩子做决定。
- 自称「乐乐」，不要要求说「你好乐乐」。`;
  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `本页：${body.pageTitle}`,
    body.sceneGuideText && `场景：${body.sceneGuideText}`,
    body.previousSummary && `前情：${body.previousSummary}`,
    body.rosterHint && `当前摆放：${body.rosterHint}`,
    body.userMessage && `孩子：${body.userMessage}`,
  ]
    .filter(Boolean)
    .join("\n");

  return deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "你好" },
    ],
    { temperature: 0.65, max_tokens: 280 },
  );
}

async function buildStoryCreationExtractPageStory(body) {
  const system = `你是儿童故事创作助手「乐乐」。孩子边摆积木边零碎说话，你要从对话里整理「本页故事」，供绘本生图使用。

## 输入
- 对话记录（孩子+乐乐，可能很碎）
- 本页场景、前情、镜头里有哪些角色
- 期望缺口（角色行为/可选元素）——仅作检查清单，不要逐条问卷
- 若提供 arucoPlacement，表示摄像头已识别角色相对站位，必须写进 voiceSupplement，且 missingField 不得为 position，followUpQuestion 不得追问站位

## 输出（只输出 JSON，无 markdown）
{
  "voiceSupplement": "给生图用的连贯中文，80～180字，写清角色在做什么、相对位置、本页道具；优先用对话里已有内容，不足时用场景合理补全",
  "recapLine": "给孩子听的口头复述，2～3句，「我听说是…」口吻，温暖简短",
  "missingField": "none|behavior|position|element",
  "followUpQuestion": "若仍缺某角色「在干什么」等关键事实，只写 ONE 开放式短问；够用了则空字符串",
  "conversationDone": true/false
}

规则：
- conversationDone=true 当 voiceSupplement 已能支撑画这一页（至少主要角色有行为）
- followUpQuestion 最多问一件事，口语化，不要说「第2题」
- 对话已有足够信息时 missingField=none，followUpQuestion 留空
- 不要编造与场景、前情矛盾的剧情`;

  const gaps = Array.isArray(body.gaps) ? body.gaps : [];
  let gapBlock = "";
  gaps.forEach((g, i) => {
    gapBlock += `${i + 1}. kind=${g.kind || ""}; role=${g.roleName || ""}; hint=${g.fallbackQuestion || ""}\n`;
  });

  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `本页：${body.pageTitle}`,
    body.sceneGuideText && `场景：${body.sceneGuideText}`,
    body.previousSummary && `前情：${body.previousSummary}`,
    body.rosterHint && `当前摆放：${body.rosterHint}`,
    body.detectedRoles && `镜头角色：${body.detectedRoles}`,
    body.arucoPlacement && `摄像头识别站位：${body.arucoPlacement}`,
    gapBlock && `期望缺口：\n${gapBlock}`,
    body.conversationLog && `对话记录：\n${body.conversationLog}`,
    "",
    "请整理本页故事 JSON。",
  ]
    .filter(Boolean)
    .join("\n");

  const raw = await deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "请整理" },
    ],
    { temperature: 0.45, max_tokens: 720 },
  );
  const obj = parseJsonObject(raw, "extract-page-story");
  const voiceSupplement = String(obj.voiceSupplement || "").trim();
  const recapLine = String(obj.recapLine || "").trim();
  const missingField = String(obj.missingField || "none").trim().toLowerCase();
  const followUpQuestion = String(obj.followUpQuestion || "").trim();
  let done = Boolean(obj.conversationDone);
  if (voiceSupplement.length >= 12 && !followUpQuestion) done = true;
  if (!voiceSupplement && !followUpQuestion) done = false;
  return {
    voiceSupplement,
    recapLine: recapLine || voiceSupplement,
    missingField,
    followUpQuestion,
    conversationDone: done,
  };
}

async function buildStoryCreationWaitNarration(body) {
  const system = `你是「乐乐」。正在为孩子生成绘本插画，请用 2～3 句口语安抚等待，并简短复述本页即将画出的情节。不要提「AI」或「生图」。`;
  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `本页：${body.pageTitle}`,
    body.pageSummary && `本页情节：${body.pageSummary}`,
  ]
    .filter(Boolean)
    .join("\n");

  return deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "请说等待时的旁白" },
    ],
    { temperature: 0.6, max_tokens: 200 },
  );
}

async function buildStoryCreationPageRecap(body) {
  const system = `你是「乐乐」。本页绘本刚生成完，用 2～3 句鼓励孩子，并串联「到目前为止的故事」。语气温暖。`;
  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.pageTitle && `刚完成：${body.pageTitle}`,
    body.pageSummary && `本页：${body.pageSummary}`,
    body.storySoFar && `到目前为止：${body.storySoFar}`,
  ]
    .filter(Boolean)
    .join("\n");

  return deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "请说页末小结" },
    ],
    { temperature: 0.6, max_tokens: 260 },
  );
}

async function buildStoryCreationBranchHint(body) {
  const system = `根据本页孩子回答，若对下一页剧情有明确暗示，输出一句下一页情境提示（20字内）；若无则输出空字符串。只输出这一句，不要解释。`;
  const user = [
    body.storyTitle && `故事：${body.storyTitle}`,
    body.nextPageTitle && `下一页：${body.nextPageTitle}`,
    body.pageSummary && `本页情节：${body.pageSummary}`,
  ]
    .filter(Boolean)
    .join("\n");

  const raw = await deepseekChat(
    [
      { role: "system", content: system },
      { role: "user", content: user || "无" },
    ],
    { temperature: 0.4, max_tokens: 80 },
  );
  return raw.replace(/^["'「]|["'」]$/g, "").trim();
}

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
  buildStoryCreationReply,
  buildStoryCreationPageSummary,
  buildStoryCreationPageCaption,
  buildStoryCreationFreeChat,
  buildStoryCreationExtractPageStory,
  buildStoryCreationWaitNarration,
  buildStoryCreationPageRecap,
  buildStoryCreationBranchHint,
  refineChildAnswer,
  refineStoryCreationAnswer,
  hasDeepSeek,
};
