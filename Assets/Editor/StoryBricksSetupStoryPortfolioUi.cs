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

    [MenuItem("StoryBricks/故事库/添加「我的故事」按钮到场景")]
    public static void AddStoryLibraryMyStoriesButtonToScene()
    {
        var portfolio = Object.FindObjectOfType<BrickPortfolioRoot>();
        if (portfolio == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 BrickPortfolioRoot。", "好的");
            return;
        }

        if (portfolio.portfolioKind != BrickPortfolioRoot.PortfolioKind.StoryLibrary)
        {
            EditorUtility.DisplayDialog(
                "不是故事库",
                "此菜单仅用于 StorySummary（故事库）场景。",
                "好的");
            return;
        }

        var canvas = portfolio.pageView?.canvas ?? Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("未找到 Canvas", "请先在场景里摆放 Canvas。", "好的");
            return;
        }

        EnsureStoryLibraryMyStoriesButton(canvas.transform, portfolio);
        EditorSceneManager.MarkSceneDirty(portfolio.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;

        EditorUtility.DisplayDialog(
            "已添加",
            "已在 Canvas 下创建 MyStoriesButton（若原先不存在）。\n\n" +
            "可在 Hierarchy 直接拖拽、改大小与文字；运行时只绑定点击，不会重排布局。",
            "好的");
    }

    [MenuItem("StoryBricks/故事作品集/添加导航按钮到场景（积木库 / 开始创作）")]
    public static void AddStoryWorksNavButtonsToScene()
    {
        var portfolio = Object.FindObjectOfType<BrickPortfolioRoot>();
        if (portfolio == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 BrickPortfolioRoot。", "好的");
            return;
        }

        if (portfolio.portfolioKind != BrickPortfolioRoot.PortfolioKind.StoryWorks)
        {
            EditorUtility.DisplayDialog(
                "不是 StoryWorks",
                "此菜单仅用于故事作品集（StoryWorks）场景。",
                "好的");
            return;
        }

        var canvas = portfolio.pageView?.canvas ?? Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("未找到 Canvas", "请先在场景里摆放 Canvas。", "好的");
            return;
        }

        EnsureStoryWorksNavButtons(canvas.transform, portfolio);
        EditorSceneManager.MarkSceneDirty(portfolio.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;

        EditorUtility.DisplayDialog(
            "已添加",
            "已在 Canvas 下创建 BrickLibraryButton / StartCreationButton（若原先不存在）。\n\n" +
            "可在 Hierarchy 直接拖拽、改大小与文字；运行时只绑定点击，不会重排布局。",
            "好的");
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
        if (portfolio.portfolioKind == BrickPortfolioRoot.PortfolioKind.StoryLibrary)
            EnsureStoryLibraryMyStoriesButton(canvas.transform, portfolio);
        else if (portfolio.portfolioKind == BrickPortfolioRoot.PortfolioKind.StoryWorks)
            EnsureStoryWorksNavButtons(canvas.transform, portfolio);
        portfolio.applyRuntimeLayout = false;
        EditorUtility.SetDirty(portfolio);
        EditorSceneManager.MarkSceneDirty(portfolio.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;

        EditorUtility.DisplayDialog(
            "已挂载",
            "已在场景绑定 StoryLibraryPageView。\n\n" +
            "可在 Hierarchy 直接编辑 Canvas 下的 HeaderTitle、ScrollView、BackButton 等。\n" +
            "StoryLibrary 还会自动添加 MyStoriesButton（若缺失）。\n" +
            "StoryWorks 还会自动添加 BrickLibraryButton / StartCreationButton（若缺失）。\n" +
            "运行时不会重排布局（applyRuntimeLayout = false）。",
            "好的");
    }

    public static void EnsureStoryLibraryMyStoriesButton(Transform canvasTransform, BrickPortfolioRoot portfolio)
    {
        if (portfolio == null || canvasTransform == null)
            return;
        if (portfolio.portfolioKind != BrickPortfolioRoot.PortfolioKind.StoryLibrary ||
            !portfolio.showMyStoriesButton)
            return;

        var view = canvasTransform.GetComponent<StoryLibraryPageView>();
        var existingTf = canvasTransform.Find("MyStoriesButton");
        if (existingTf == null)
        {
            var btn = StoryLibraryUiBuilder.CreateMyStoriesButton(
                canvasTransform,
                portfolio.myStoriesButtonLabel);
            Undo.RegisterCreatedObjectUndo(btn.gameObject, "Create MyStoriesButton");
            if (view != null)
            {
                view.myStoriesButton = btn;
                EditorUtility.SetDirty(view);
            }
        }
        else if (view != null && view.myStoriesButton == null)
        {
            view.myStoriesButton = existingTf.GetComponent<Button>();
            EditorUtility.SetDirty(view);
        }
    }

    public static void EnsureStoryWorksNavButtons(Transform canvasTransform, BrickPortfolioRoot portfolio)
    {
        if (portfolio == null || canvasTransform == null)
            return;
        if (portfolio.portfolioKind != BrickPortfolioRoot.PortfolioKind.StoryWorks)
            return;

        var view = canvasTransform.GetComponent<StoryLibraryPageView>();
        int column = canvasTransform.Find("BackButton") != null ? 1 : 0;

        if (portfolio.showBrickLibraryButton)
        {
            var brickTf = canvasTransform.Find("BrickLibraryButton");
            if (brickTf == null)
            {
                var btn = StoryLibraryUiBuilder.CreateTopLeftNavButton(
                    canvasTransform,
                    "BrickLibraryButton",
                    portfolio.brickLibraryButtonLabel,
                    column);
                Undo.RegisterCreatedObjectUndo(btn.gameObject, "Create BrickLibraryButton");
                if (view != null)
                {
                    view.brickLibraryButton = btn;
                    EditorUtility.SetDirty(view);
                }
            }
            else if (view != null && view.brickLibraryButton == null)
            {
                view.brickLibraryButton = brickTf.GetComponent<Button>();
                EditorUtility.SetDirty(view);
            }
        }

        if (portfolio.showStartCreationButton)
        {
            var startTf = canvasTransform.Find("StartCreationButton");
            if (startTf == null)
            {
                var btn = StoryLibraryUiBuilder.CreateStartCreationButton(
                    canvasTransform,
                    portfolio.startCreationButtonLabel);
                Undo.RegisterCreatedObjectUndo(btn.gameObject, "Create StartCreationButton");
                if (view != null)
                {
                    view.startCreationButton = btn;
                    EditorUtility.SetDirty(view);
                }
            }
            else if (view != null && view.startCreationButton == null)
            {
                view.startCreationButton = startTf.GetComponent<Button>();
                EditorUtility.SetDirty(view);
            }
        }
    }
}
#endif
