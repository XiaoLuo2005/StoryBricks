using UnityEngine;

/// <summary>
/// 一条故事的 Demo/正式数据包：封面、绘本前情页、搭建场景。放在 Resources/Stories 下可自动加载。
/// </summary>
[CreateAssetMenu(fileName = "NewStory", menuName = "StoryBricks/Story Definition")]
public class StoryDefinition : ScriptableObject
{
    public string storyId = "story_id";
    public string title = "故事标题";
    [TextArea(2, 6)]
    public string synopsisText = "";
    public Sprite thumbnail;
    [Tooltip("绘本前情，按页顺序")]
    public Sprite[] prologuePages;
    [Tooltip("留空则用 StoryPrologue")]
    public string prologueSceneName = "";
    [Tooltip("搭建/教程场景名，须在 Build Settings 中")]
    public string buildSceneName = "RabbitTutorial";
}
