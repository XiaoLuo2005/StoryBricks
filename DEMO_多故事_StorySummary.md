# 多故事故事库（只用 StorySummary）

## 流程

`StartScene` → `StorySummary`（多张 StoryCard）→ 点「选择」→ `StoryPrologue`（绘本）→ `RabbitTutorial`

## 一次性搭建（Unity 菜单）

1. 等脚本编译完。
2. **StoryBricks → 搭建 StorySummary 多故事库**
3. 会生成：ScrollView、StoryLibrary、StoryCard.prefab（含 **Story Card View**，支持 **TMP**）。

## 加一条故事（以后重复做）

1. **Project** 里 `Assets/Resources/Stories/` 右键 → **Create → StoryBricks → Story Definition**。
2. 填写：
   - **Story Id**：英文唯一，如 `story_wolf`
   - **Title**：列表上显示的名字
   - **Thumbnail**：故事库卡片封面（Sprite）
   - **Prologue Pages**：绘本前情页图（按顺序，可多张）
   - **Build Scene Name**：搭建场景名（须在 Build Settings）
3. 打开 **StorySummary** → **StoryLibrary** → **Story Catalog** → **Stories**：
   - **Size +1**，把新资产拖进新格子。
4. 保存场景。

也可不拖 Catalog：只要 `.asset` 放在 `Resources/Stories/` 下，运行时会自动加载（Catalog 为空时）。

## 改卡片外观

双击 **`Assets/Prefabs/UI/StoryCard.prefab`**（封面区、TMP 标题、「选择」按钮）。

## 改故事库页面布局

打开 **StorySummary** 场景，改 Canvas / ScrollView / Content 上的 **Grid Layout Group**（一行几张、格子大小）。

## 注意

- **不要**用 `BrickLibrary` 场景；故事集合只在 **StorySummary**。
- 场景里若还留着以前手摆的一张 **StoryCard**，会和自动生成的卡重复；搭建菜单会删掉旧卡，以 Prefab 生成为准。
- 第一条示例故事：**Story_TortoiseHare**（龟兔赛跑）。
