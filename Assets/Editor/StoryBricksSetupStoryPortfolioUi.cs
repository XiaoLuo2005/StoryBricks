#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>StorySummary / StoryWorks 场景 UI 可视化挂载（与 BrickLibrary 同一套 StoryLibraryPageView）。</summary>
public static class StoryBricksSetupStoryPortfolioUi
{
    const string SummaryScenePath = "Assets/Scenes/StorySummary.unity";
    const string WorksScenePath = "Assets/Scenes/StoryWorks.unity";

    [MenuItem("StoryBricks/故事库/保留现有布局并挂载")]
    public static void WireStorySummaryScene()
    {
        WireActivePortfolioScene("StoryLibrary");
    }

    [MenuItem("StoryBricks/故事作品集/保留现有布局并挂载")]
    public static void WireStoryWorksScene()
    {
        WireActivePortfolioScene("StoryWorksPortfolio");
    }

    [MenuItem("StoryBricks/故事库/打开 StorySummary 并挂载可视化 UI")]
    public static void OpenSummaryAndWire()
    {
        EditorSceneManager.OpenScene(SummaryScenePath, OpenSceneMode.Single);
        WireActivePortfolioScene("StoryLibrary");
    }

    [MenuItem("StoryBricks/故事作品集/打开 StoryWorks 并挂载可视化 UI")]
    public static void OpenWorksAndWire()
    {
        EditorSceneManager.OpenScene(WorksScenePath, OpenSceneMode.Single);
        WireActivePortfolioScene("StoryWorksPortfolio");
    }

    static void WireActivePortfolioScene(string expectedRootName)
    {
        var portfolio = Object.FindObjectOfType<BrickPortfolioRoot>();
        if (portfolio == null)
        {
            EditorUtility.DisplayDialog(
                "未找到 Root",
                $"当前场景里没有 BrickPortfolioRoot（期望 {expectedRootName}）。",
                "好的");
            return;
        }

        var canvas = portfolio.pageView?.canvas ?? Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("未找到 Canvas", "请先在场景里摆放 Canvas 与 ScrollView。", "好的");
            return;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        StoryBricksSetupBrickLibraryUi.WireCanvasToPortfolio(portfolio, canvas.gameObject);
        portfolio.applyRuntimeLayout = false;
        EditorUtility.SetDirty(portfolio);
        EditorSceneManager.MarkSceneDirty(portfolio.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;

        EditorUtility.DisplayDialog(
            "已挂载",
            "已在场景绑定 StoryLibraryPageView。\n\n" +
            "可在 Hierarchy 直接编辑 Canvas 下的 HeaderTitle、ScrollView、BackButton 等。\n" +
            "运行时不会重排布局（applyRuntimeLayout = false）。",
            "好的");
    }
}
#endif
