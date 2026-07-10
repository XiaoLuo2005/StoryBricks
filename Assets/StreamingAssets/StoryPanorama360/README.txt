StoryBricks 360 全景资源说明
================================

在「我的故事 → 阅读」中，点右上角「360° 全景」可陀螺仪环视（与手机 360 视频类似）。

## 自动生成（推荐）

完成故事创作时，若 `StoryCreationPageBootstrap.generatePanoramaAfterPage` 为 true，
且生图网关已启动（`POST /generate-panorama`），每页绘本成图后会**再生成一张 360 全景图**，
保存到绘本目录：`page_00_panorama.png`、`page_01_panorama.png` …

需配置与绘本相同的 `DASHSCOPE_API_KEY`，默认尺寸 1536×768（2:1）。

## 手动放置

每页需 equirectangular（2:1）MP4 或 JPG/PNG。任选一种：

1) 与绘本 PNG 同目录（推荐）
   路径：persistentDataPath/CompletedStories/<saveId>/
   命名：page_00.png → page_00_panorama.jpg 或 page_00_panorama.png

2) 兼容旧命名
   page_00_pano.jpg / page_00_pano.png

3) 按 pageId 命名（同 save 目录）
   例如：p1_start.jpg

4) 工程内置演示（StreamingAssets）
   Assets/StreamingAssets/StoryPanorama360/<storyId>/<pageId>.jpg

5) 在 story.json 里显式写字段 panoramaImageFile

视频建议：H.264 MP4，2048×1024 或 3840×1920，15–30 秒循环。
全景图建议：JPG/PNG，2:1，2048×1024 起。

PC 编辑器无陀螺仪时：按住鼠标左键拖拽环视。

## 说明

本项目阅读页**仅支持 360° 全景环视**，不含头显 VR / 立体分屏剧场模式。
