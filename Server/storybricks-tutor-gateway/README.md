# StoryBricks AI 助教网关

Node.js **18+**。本服务**仅依赖 `busboy`**（解析 multipart），使用 Node 内置 `http`。

**仅对接阿里云灵积 DashScope**：Qwen3-ASR + 通义千问（兼容模式 HTTP）+ CosyVoice TTS。

Unity 只访问本服务，**不要把 `DASHSCOPE_API_KEY` 写进 Unity 工程**。

---

若 **`npm install` 报 ETIMEDOUT**（校园网/国内访问 npm 慢），可换镜像后再装：

```bash
npm config set registry https://registry.npmmirror.com
npm install
```

装成功后再执行 `npm start`。镜像仅影响本机 npm 下载包，与 DashScope 无关。

## 从步骤图 PNG 自动生成三份助教 txt（可选）

在**含步骤说明图**的文件夹里（例如 `Assets/Step/Rabbit`，多张 `*_1x.png`），可用视觉模型一次性生成与 Unity 对齐的三份文件：

- `{prefix}TutorOverview.txt`
- `{prefix}StepHints.txt`
- `{prefix}StepDetails.txt`

默认 `prefix` 为「文件夹名首字母大写 + `Tutorial`」（如 `Rabbit` → `RabbitTutorial`），与现有兔子资源命名一致。

脚本会把**最后一级文件夹名**作为「在拼什么」的语义线索写入提示（如 `Rabbit` 会映射为中文主题「兔子」；中文目录名则原样作主题）。目录名无法识别时由模型结合图片推断；也可用 **`--theme=兔子`** 强制指定中文主题。

```bash
cd Server/storybricks-tutor-gateway
set DASHSCOPE_API_KEY=sk-你的灵积Key
node tools/generate-tutorial-tutor-texts.mjs "G:/1/1A大三下/人机交互/StoryBricks/Assets/Step/Rabbit"
```

（PowerShell：`$env:DASHSCOPE_API_KEY="..."`）

可选：`--prefix=OtherTutorial` 改输出文件名前缀；`--model=qwen-vl-plus` 或控制台支持的其它 VL 模型；`--theme=兔子` 强制指定中文造型名（覆盖目录名推断）；`--dry-run` 只统计体积不调用 API。

生成脚本会**尽量**用可选依赖 `sharp` 把每张 PNG 缩放到长边 ≤ `TUTOR_GEN_MAX_EDGE`（默认 1200），以控制请求体积；`npm install` 时会尝试安装 `sharp`（失败则仍用原图，可能触顶 API 限制）。

依赖与网关相同的环境变量；生成后请在 Unity 里检查文案，必要时手改。

## 启动

```bash
cd Server/storybricks-tutor-gateway
# 若曾安装过旧版 express，建议删掉 node_modules 再装
# rm -r node_modules
npm install
set DASHSCOPE_API_KEY=sk-b981173cf88d49b7825724b006a1eb08
npm start
```

（PowerShell：`$env:DASHSCOPE_API_KEY="..."`）

获取 API Key：[阿里云百炼 / Model Studio](https://help.aliyun.com/zh/model-studio/get-api-key)

### 常用可选环境变量


| 变量                          | 默认                                                  | 说明                     |
| --------------------------- | --------------------------------------------------- | ---------------------- |
| `PORT`                      | `8787`                                              | 监听端口                   |
| `DASHSCOPE_COMPAT_BASE`     | `https://dashscope.aliyuncs.com/compatible-mode/v1` | 兼容模式基址（北京）；其他地区见官方文档   |
| `DASHSCOPE_ASR_MODEL`       | `qwen3-asr-flash`                                   | 语音识别模型                 |
| `DASHSCOPE_CHAT_MODEL`      | `qwen-turbo`                                        | 对话模型，可改为 `qwen-plus` 等 |
| `DASHSCOPE_TTS_MODEL`       | `cosyvoice-v3-flash`                                | 语音合成模型                 |
| `DASHSCOPE_TTS_VOICE`       | `longanyang`                                        | 音色，以控制台「音色列表」为准        |
| `DASHSCOPE_TTS_SAMPLE_RATE` | `24000`                                             | 采样率                    |


### API

- `GET /health` — 返回 `hasDashScopeKey`、`dashCompatBase` 等  
- `POST /api/tutor/text` — JSON 必填：`tutorialTitle`, `stepIndex`, `stepCount`, `userMessage`  
  - 可选：`stepHint`（短句）  
  - 可选：`tutorialTutorOverview`（套装总览文档，UTF-8 文本；网关侧最多约 12000 字）  
  - 可选：`stepGoal`, `stepPartsUsed`, `stepKeyActions`, `stepPitfalls`（本步结构化说明，与 Unity `TutorialStepTutorDetail` 对应）  
- `POST /api/tutor/voice` — `multipart/form-data`：字段 `audio`（WAV）+ 与上表相同的文本字段（无 `userMessage`，由 ASR 得到）

CosyVoice 非流式返回音频 **OSS 临时 URL**，由网关下载后转 **Base64 WAV** 再给 Unity。

---

## Unity 侧

场景里 `TutorialStepsPageBootstrap`：**Enable Voice Tutor** + **Tutor Gateway Base Url** = `http://127.0.0.1:8787`（或你部署的地址）。网关已启动且配置了 `DASHSCOPE_API_KEY` 即可。

在 **`TutorialStepsConfig`** 里可配置：

- **`tutorialTutorOverviewText`**：一份 TextAsset（如 `.txt`），套装总览、零件表摘要、安全须知等；会随每次对话发给网关。  
- **`stepTutorDetails`**：与 `steps` 等长的数组，每步填写 `TutorialStepTutorDetail`（目标、零件、关键动作、易错点）。  
- **`stepHints`**：仍可保留为简短补充，会与结构化说明一并写入 system 提示。