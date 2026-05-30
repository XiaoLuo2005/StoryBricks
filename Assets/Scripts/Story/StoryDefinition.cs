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
}
