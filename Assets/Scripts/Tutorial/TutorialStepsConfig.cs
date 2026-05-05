using UnityEngine;

[CreateAssetMenu(menuName = "StoryBricks/Tutorial Steps Config", fileName = "TutorialStepsConfig")]
public class TutorialStepsConfig : ScriptableObject
{
    public string title = "拼装教程";
    [Tooltip("顶栏「返回」加载的场景名（须在 Build Settings 中）")]
    public string portfolioSceneName = "BrickLibrary";
    [Tooltip("按顺序排列的步骤图")]
    public Sprite[] steps;
}
