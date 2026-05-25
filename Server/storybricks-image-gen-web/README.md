# StoryBricks 本地生图网页（替代第三方网页）

与 Unity `LocalImageGenClient` 相同接口：`POST` JSON 到 `/generate`，字段 `prompt`、`model`、`size`（默认 `1024*1024`）、`n`；响应里使用 `image_url` 展示图片。

## 为什么需要 `proxy.mjs`

浏览器从本机页面请求公网 `http://39.97...` 时，若服务端未返回 CORS 头，会报跨域错误。本地代理把请求转到上游，页面只访问 `http://127.0.0.1:8765/generate`，无跨域问题。

## 使用步骤

1. 安装 **Node.js 18+**。
2. 在终端进入本目录：

   `cd Server/storybricks-image-gen-web`

3. （可选）指定上游地址，默认已写阿里云示例：

   PowerShell: `$env:STORYBRICKS_GENERATE_URL="http://你的服务器:端口/generate"`

4. 启动：

   `node proxy.mjs`

5. 浏览器打开：**http://127.0.0.1:8765/** ，填写提示词后点「生成」。

改端口：`$env:PORT=9000` 后再 `node proxy.mjs`。

若浏览器出现 `{"detail":"Method Not Allowed"}`：说明在地址栏打开了 **`…/generate`**（浏览器用 **GET**）。请改为打开首页 **`http://127.0.0.1:8765/`**（末尾是 `/`），在页面里点「生成」。

## 本机 Python 生图服务

若你的 `generate` 跑在本机（例如 `http://127.0.0.1:某端口/generate`），把 `STORYBRICKS_GENERATE_URL` 设成该地址即可。
