using System;
using UnityEngine;

/// <summary>
/// 一条故事的 Demo/正式数据包：封面、绘本前情页、关联积木作品。放在 Resources/Stories 下可自动加载。
/// </summary>
[CreateAssetMenu(fileName = "NewStory", menuName = "StoryBricks/Story Definition")]
public class StoryDefinition : ScriptableObject
{
    [Serializable]
    public class StoryBrickWorkEntry
    {
        public string workId = "";
        public string title = "未命名作品";
        public string tutorialSceneName = "";
        public Sprite thumbnail;
    }

    /// <summary>ArUco ID 对应的标准角色参考图，供分页生图时锁定外貌。</summary>
    [Serializable]
    public class CharacterReferenceEntry
    {
        [Tooltip("ArUco 标记 ID，如 1=兔子、2=乌龟")]
        public int markerId;

        [Tooltip("角色标准形象（单人、风格统一）")]
        public Sprite referenceSprite;

        [Tooltip("Prompt 中的角色名，如「兔子」")]
        public string roleName = "";
    }

    /// <summary>
    /// 单页故事创作配置：固定背景、情境引导、本页识别期望。按顺序组成 P1/P2/P3… 分页创作流程。
    /// </summary>
    [Serializable]
    public class StoryPageDefinition
    {
        [Tooltip("页唯一标识，如 p1_start；供缓存与日志使用")]
        public string pageId = "p1";

        [Tooltip("页标题，如「起跑线」；创作场景顶栏或调试显示")]
        public string pageTitle = "第 1 页";

        [Tooltip("本页固定背景（起跑线 / 大树 / 终点等）")]
        public Sprite backgroundSprite;

        [TextArea(2, 6)]
        [Tooltip("屏幕情境引导文案")]
        public string sceneGuideText = "";

        [TextArea(2, 6)]
        [Tooltip("语音引导话术（TTS 或状态机朗读）")]
        public string voiceGuideText = "";

        [Tooltip("本页期望出现的角色 ArUco ID（1–20）；留空表示不校验")]
        public int[] requiredCharacterIds;
    }

    public string storyId = "story_id";
    public string title = "故事标题";
    [TextArea(2, 6)]
    public string synopsisText = "";
    public Sprite thumbnail;
    [Tooltip("绘本前情，按页顺序")]
    public Sprite[] prologuePages;
    [Tooltip("留空则用 StoryPrologue")]
    public string prologueSceneName = "";
    [Tooltip("绘本后进入的故事作品集场景；留空则用 StoryWorks")]
    public string storyWorksSceneName = "";
    [Tooltip("本故事包含的积木作品（如龟兔赛跑：兔子 + 乌龟）")]
    public StoryBrickWorkEntry[] works;

    [Header("分页故事创作")]
    [Tooltip("ArUco ID → 角色标准形象，供 img2img 锁定外貌")]
    public CharacterReferenceEntry[] characterReferences;

    [TextArea(2, 4)]
    [Tooltip("每页生图 Prompt 前缀（画风统一）")]
    public string stylePromptPrefix = "儿童绘本水彩插画，温暖明亮色调，横版16比9";

    [Tooltip("按顺序的分页创作配置（P1 起跑 → P2 大树 → P3 终点等）")]
    public StoryPageDefinition[] creationPages;

    [Tooltip("分页创作场景名；留空则待 StoryCreation 场景落地后填写")]
    public string creationSceneName = "";
}
