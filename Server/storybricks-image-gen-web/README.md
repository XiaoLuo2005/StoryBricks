# StoryBricks 生图网关（wan2.6 + img2img）

对接阿里云百炼 **wan2.6-image**，为 Unity `LocalImageGenClient` 提供统一 HTTP 接口。

## 接口

`POST /generate`（JSON）

| 字段 | 必填 | 说明 |
|------|------|------|
| `prompt` | 是 | 生图/编辑指令 |
| `model` | 否 | 默认 `wan2.6-image` |
| `size` | 否 | 默认 `1920*1080`（16:9，也支持 `1K` / `2K`） |
| `n` | 否 | 生成张数，默认 `1` |
| `reference_images` | 否 | **1~4 张**参考图（HTTP URL 或 `data:image/png;base64,...`） |

### 模式

- **有 `reference_images`**：同步 **图像编辑（img2img）**，传入**角色标准形象图**（非页背景）
- **无 `reference_images`**：异步 **文生图**，服务端轮询 task 后返回 `image_url`

### 响应示例

```json
{
  "task_id": "xxx",
  "image_url": "https://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/....png",
  "model": "wan2.6-image",
  "mode": "image_edit"
}
```

`mode` 为 `image_edit` 或 `text_to_image`。

## 部署（生产 / 云服务器）

1. 安装 **Node.js 18+**
2. 进入目录并配置密钥：

   ```powershell
   cd Server/storybricks-image-gen-web
   copy .env.example .env
   # 编辑 .env，填入 DASHSCOPE_API_KEY
   ```

3. 启动：

   ```powershell
   node server.mjs
   ```

4. 健康检查：`GET http://<host>:8800/health`

默认监听 `0.0.0.0:8800`。创作页 `StoryCreationPageBootstrap` 默认连 `http://127.0.0.1:8800/generate`。

### img2img 请求体过大（nginx 413）

角色参考图以 base64 上传，多张大图易超过 nginx `client_max_body_size`（常见 1MB）。

- **本机调试**：用本机 `server.mjs`（默认允许 40MB），Unity 填 `127.0.0.1:8800`
- **客户端**：`LocalImageGenClient.maxReferenceUploadEdge` 默认 512，自动缩小参考图
- **云 nginx**：需增大 `client_max_body_size`（如 `20m`）

## 本地调试页（CORS 代理）

浏览器直连公网 API 可能跨域，可用 `proxy.mjs`：

```powershell
cd Server/storybricks-image-gen-web
$env:STORYBRICKS_GENERATE_URL="http://127.0.0.1:8800/generate"
node proxy.mjs
```

浏览器打开 **http://127.0.0.1:8765/** ，可上传参考图测试 img2img。

若地址栏直接打开 `…/generate` 会报 Method Not Allowed（浏览器发 GET）；请打开首页 `/` 再点「生成」。

## Unity 用法

`LocalImageGenClient` 已支持：

```csharp
// 文生图
client.GenerateImage("儿童绘本，兔子在起跑线");

// img2img：角色标准形象参考图（StoryDefinition.characterReferences）
client.GenerateImageFromSprites(prompt, characterReferenceSprites);
```

创作页 `StoryCreationPageBootstrap` 会自动：识别 ArUco → 匹配 `characterReferences` → P2+ 追加 P1 锚图 → img2img。

Unity 菜单 **StoryBricks → 龟兔赛跑 → 绑定角色参考图** 可写入 `rabbit.png` / `tortoise.png`。

Inspector 里可配置 `debugReferenceImages`，右键 **Generate Image With References** 调试。

## 环境变量

| 变量 | 默认 | 说明 |
|------|------|------|
| `DASHSCOPE_API_KEY` | — | **必填**，百炼 API Key |
| `PORT` | `8800` | 监听端口 |
| `HOST` | `0.0.0.0` | 监听地址 |
| `DASHSCOPE_BASE_URL` | `https://dashscope.aliyuncs.com/api/v1` | 百炼 API 基址 |
| `POLL_INTERVAL_MS` | `2000` | 文生图轮询间隔 |
| `POLL_MAX_ATTEMPTS` | `90` | 文生图最大轮询次数 |
