using UnityEngine;

[CreateAssetMenu(menuName = "StoryBricks/Tutorial Steps Config", fileName = "TutorialStepsConfig")]
public class TutorialStepsConfig : ScriptableObject
{
    public string title = "拼装教程";
    [Tooltip("顶栏「返回」加载的场景名（须在 Build Settings 中）")]
    public string portfolioSceneName = "BrickLibrary";
    [Tooltip("按顺序排列的步骤图")]
    public Sprite[] steps;

    [Tooltip("与 steps 等长时可一一对应；某步无文案可留空。会发给 AI 助教作为本步提示")]
    public string[] stepHints;

    [Tooltip("可选；与 steps 等长。每步目标、零件、动作、易错点——比纯 stepHints 更利于模型理解")]
    public TutorialStepTutorDetail[] stepTutorDetails;

    [Tooltip("可选；套装总览：名称、零件表摘要、安全须知等（TextAsset，UTF-8）。会随每次对话发给网关")]
    public TextAsset tutorialTutorOverviewText;

    [Tooltip("可选；非空时按行解析为各步短提示（# 行为注释），覆盖下方 stepHints 数组")]
    public TextAsset stepHintsSourceText;

    [Tooltip("可选；用 :::STEP::: 分步，块内 GOAL:/PARTS:/ACTIONS:/PITFALLS:，覆盖下方 stepTutorDetails")]
    public TextAsset stepTutorDetailsSourceText;

    [Tooltip("可选；整模 3D 预览用。将 Studio 导出的模型做成 Prefab 拖到这里；留空则教程页不显示「3D 预览」")]
    public GameObject previewModelPrefab;

    [Tooltip("可选；左下角 Lottie 吉祥物。请用 UTF-8 的 TextAsset（建议扩展名 .txt，内容为 Lottie JSON）。留空则使用 Resources/TutorialMascot/AnimaBotLottie")]
    public TextAsset mascotLottieJsonText;
}
