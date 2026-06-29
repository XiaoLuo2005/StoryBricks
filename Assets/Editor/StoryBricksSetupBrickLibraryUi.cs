#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StoryBricksSetupBrickLibraryUi
{
    const string PrefabPath = "Assets/Prefabs/UI/BrickLibraryPage.prefab";
    const string ResourcesPrefabPath = "Assets/Resources/UI/BrickLibraryPage.prefab";
    const string ScenePath = "Assets/Scenes/BrickLibrary.unity";

    [MenuItem("StoryBricks/BrickLibrary/创建 BrickLibrary UI Prefab")]
    public static void CreatePrefab()
    {
        StoryBricksSetupStorySummary.EnsureCardPrefabPublic();

        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Resources/UI");

        var view = StoryLibraryUiBuilder.BuildBrickLibraryPageView(null);
        view.gameObject.SetActive(true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath);
        Object.DestroyImmediate(view.gameObject);

        SyncPrefabToResources();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "BrickLibrary UI Prefab 已创建",
            "已生成：\n" +
            $"• {PrefabPath}\n" +
            $"• {ResourcesPrefabPath}\n\n" +
            "可在 Prefab 模式调整 HeaderTitle / ScrollView / BackButton / StoryLibraryDecor。",
            "好的");

        Selection.activeObject = prefab;
    }

    [MenuItem("StoryBricks/BrickLibrary/当前场景挂载可视化 UI（保留现有布局）")]
    public static void WireExistingScene()
    {
        var portfolio = Object.FindObjectOfType<BrickPortfolioRoot>();
        if (portfolio == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 BrickPortfolioRoot。", "好的");
            return;
        }

        var canvas = portfolio.pageView?.canvas ?? Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("未找到 Canvas", "请先摆放 Canvas，或运行「创建 BrickLibrary UI Prefab」后实例化。", "好的");
            return;
        }

        WireCanvasToPortfolio(portfolio, canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;

        EditorUtility.DisplayDialog(
            "已挂载",
            "BrickLibrary 已绑定 StoryLibraryPageView。\n\n" +
            "现在可在 Hierarchy 里直接改 Canvas / HeaderTitle / ScrollView / BackButton，" +
            "以及 StoryLibraryDecor 背景。",
            "好的");
    }

    [MenuItem("StoryBricks/BrickLibrary/打开场景并挂载可视化 UI")]
    public static void OpenSceneAndWire()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("场景不存在", $"未找到 {ScenePath}", "好的");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var portfolio = Object.FindObjectOfType<BrickPortfolioRoot>();
        if (portfolio == null)
        {
            var lib = new GameObject("BrickPortfolio");
            portfolio = lib.AddComponent<BrickPortfolioRoot>();
            portfolio.portfolioKind = BrickPortfolioRoot.PortfolioKind.BrickWorks;
        }

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            CreatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<StoryLibraryPageView>(PrefabPath);
            if (prefab == null)
                return;
            var instance = (StoryLibraryPageView)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "BrickLibraryCanvas";
            canvas = instance.canvas;
        }

        WireCanvasToPortfolio(portfolio, canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;
    }

    [MenuItem("StoryBricks/BrickLibrary/将当前场景 UI 布局保存到共用 Prefab")]
    public static void ApplySceneLayoutToSharedPrefab()
    {
        var view = Object.FindObjectOfType<StoryLibraryPageView>();
        if (view == null)
        {
            EditorUtility.DisplayDialog("未找到 UI", "请先运行「当前场景挂载可视化 UI」。", "好的");
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
        {
            EditorUtility.DisplayDialog("不是 Prefab 实例", "Canvas 需为 BrickLibraryPage Prefab 实例，或先创建 Prefab。", "好的");
            return;
        }

        PrefabUtility.ApplyPrefabInstance(view.gameObject, InteractionMode.UserAction);
        SyncPrefabToResources();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("已保存", "BrickLibrary UI 排版已写入共用 Prefab。", "好的");
    }

    public static void WireCanvasToPortfolio(BrickPortfolioRoot portfolio, GameObject canvasGo)
    {
        var view = canvasGo.GetComponent<StoryLibraryPageView>();
        if (view == null)
            view = canvasGo.AddComponent<StoryLibraryPageView>();

        view.canvas = canvasGo.GetComponent<Canvas>();
        view.scrollRect = canvasGo.GetComponentInChildren<ScrollRect>(true);
        view.cardListContent = view.scrollRect != null ? view.scrollRect.content : null;

        var header = canvasGo.transform.Find("HeaderTitle");
        if (header != null)
        {
            view.headerTitleImage = header.GetComponent<Image>();
            view.headerTitle = header.GetComponent<TMPro.TextMeshProUGUI>();
        }

        var back = canvasGo.transform.Find("BackButton");
        if (back == null)
        {
            var btn = StoryLibraryUiBuilder.CreateBackButton(canvasGo.transform);
            Undo.RegisterCreatedObjectUndo(btn.gameObject, "Create BackButton");
            view.backButton = btn;
        }
        else
            view.backButton = back.GetComponent<Button>();

        view.decorRoot = EnsureDecorRoot();

        portfolio.pageView = view;
        portfolio.allowRuntimeFallbackUi = false;
        portfolio.applyRuntimeLayout = false;
        portfolio.cardListContent = view.cardListContent;
        portfolio.headerTitleTextTmp = view.headerTitle;

        EditorUtility.SetDirty(portfolio);
        EditorUtility.SetDirty(view);
    }

    static Transform EnsureDecorRoot()
    {
        var decor = GameObject.Find("StoryLibraryDecor");
        if (decor != null)
            return decor.transform;

        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.StartsWith("故事积木"))
            {
                decor = new GameObject("StoryLibraryDecor");
                root.transform.SetParent(decor.transform, true);
                return decor.transform;
            }
        }

        return StoryLibraryUiBuilder.EnsureBrickLibraryDecorVisible();
    }

    static void SyncPrefabToResources()
    {
        if (!File.Exists(PrefabPath))
            return;

        EnsureFolder("Assets/Resources/UI");
        if (File.Exists(ResourcesPrefabPath))
            AssetDatabase.DeleteAsset(ResourcesPrefabPath);
        AssetDatabase.CopyAsset(PrefabPath, ResourcesPrefabPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
