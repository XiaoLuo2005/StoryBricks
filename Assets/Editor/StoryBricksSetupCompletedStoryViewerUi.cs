#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class StoryBricksSetupCompletedStoryViewerUi
{
    const string PrefabPath = "Assets/Prefabs/UI/CompletedStoryViewerPage.prefab";
    const string ResourcesPrefabPath = "Assets/Resources/UI/CompletedStoryViewerPage.prefab";
    const string ViewerScenePath = "Assets/Scenes/CompletedStoryViewer 1.unity";

    [MenuItem("StoryBricks/我的故事/创建 CompletedStoryViewer UI Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Resources/UI");

        var view = CompletedStoryViewerUiBuilder.BuildPageView(null);
        view.gameObject.SetActive(true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath);
        Object.DestroyImmediate(view.gameObject);

        SyncPrefabToResources();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "绘本阅读 UI Prefab 已创建",
            "已生成：\n" +
            $"• {PrefabPath}\n" +
            $"• {ResourcesPrefabPath}\n\n" +
            "可在 Prefab / 场景里调整：\n" +
            "• StoryToggleButton / StoryReaderPanel / StoryCloseButton\n" +
            "• PageImage / 翻页按钮 / VR 按钮",
            "好的");

        Selection.activeObject = prefab;
    }

    [MenuItem("StoryBricks/我的故事/阅读场景保留现有布局并挂载")]
    public static void WireExistingScene()
    {
        var root = Object.FindObjectOfType<CompletedStoryViewerRoot>();
        if (root == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 CompletedStoryViewerRoot。", "好的");
            return;
        }

        CreatePrefab();
        if (root.pageView != null && PrefabUtility.IsPartOfPrefabInstance(root.pageView.gameObject))
        {
            PrefabUtility.RevertPrefabInstance(root.pageView.gameObject, InteractionMode.UserAction);
            root.pageView.EnsureStoryToggleButton();
            WireRootToPageView(root, root.pageView);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeGameObject = root.pageView.gameObject;
        }
        else
        {
            WireViewerScene(root, replaceExistingUi: false);
        }

        EditorUtility.DisplayDialog(
            "已挂载",
            "CompletedStoryViewer 已绑定场景 UI。\n\n" +
            "可在 Hierarchy 直接编辑 StoryToggleButton、StoryReaderPanel、StoryCloseButton 等。",
            "好的");
    }

    [MenuItem("StoryBricks/我的故事/阅读场景挂载可视化 UI")]
    public static void MountInActiveScene()
    {
        var root = Object.FindObjectOfType<CompletedStoryViewerRoot>();
        if (root == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 CompletedStoryViewerRoot。", "好的");
            return;
        }

        WireViewerScene(root, replaceExistingUi: root.pageView == null);
    }

    /// <summary>供批处理或搭建菜单调用：写入场景并保存。</summary>
    public static void BatchWireViewerSceneAndSave()
    {
        if (!File.Exists(ViewerScenePath))
            return;

        CreatePrefab();
        EditorSceneManager.OpenScene(ViewerScenePath, OpenSceneMode.Single);

        if (Object.FindObjectOfType<CompletedStoryViewerRoot>() == null)
            new GameObject("CompletedStoryViewerRoot", typeof(CompletedStoryViewerRoot));

        var root = Object.FindObjectOfType<CompletedStoryViewerRoot>();
        WireViewerScene(root, replaceExistingUi: true);
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("StoryBricks/我的故事/打开阅读场景并挂载可视化 UI")]
    public static void OpenViewerSceneAndMount()
    {
        if (!File.Exists(ViewerScenePath))
        {
            EditorUtility.DisplayDialog("场景不存在", $"未找到 {ViewerScenePath}", "好的");
            return;
        }

        EditorSceneManager.OpenScene(ViewerScenePath, OpenSceneMode.Single);

        if (Object.FindObjectOfType<CompletedStoryViewerRoot>() == null)
            new GameObject("CompletedStoryViewerRoot", typeof(CompletedStoryViewerRoot));

        var root = Object.FindObjectOfType<CompletedStoryViewerRoot>();
        WireViewerScene(root, replaceExistingUi: true);
    }

    [MenuItem("StoryBricks/我的故事/将阅读场景 UI 布局保存到共用 Prefab")]
    public static void ApplySceneLayoutToSharedPrefab()
    {
        var view = Object.FindObjectOfType<CompletedStoryViewerPageView>();
        if (view == null)
        {
            EditorUtility.DisplayDialog("未找到 UI", "请先运行「阅读场景挂载可视化 UI」。", "好的");
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
        {
            EditorUtility.DisplayDialog("不是 Prefab 实例", "CompletedStoryViewerCanvas 需为 Prefab 实例。", "好的");
            return;
        }

        PrefabUtility.ApplyPrefabInstance(view.gameObject, InteractionMode.UserAction);
        SyncPrefabToResources();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("已保存", "绘本阅读 UI 排版已写入共用 Prefab。", "好的");
    }

    public static void WireViewerScene(CompletedStoryViewerRoot root, bool replaceExistingUi)
    {
        if (root == null)
            return;

        if (!replaceExistingUi && TryWireExistingCanvas(root))
            return;

        if (root.pageView != null && root.pageView.gameObject.scene.IsValid())
        {
            if (!replaceExistingUi &&
                !EditorUtility.DisplayDialog("替换现有 UI", "场景里已有 pageView，要删除并重新挂载吗？", "替换", "取消"))
                return;
            Object.DestroyImmediate(root.pageView.gameObject);
            root.pageView = null;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var view = CompletedStoryViewerUiBuilder.BuildPageView(null);
        view.name = "CompletedStoryViewerCanvas";
        Undo.RegisterCreatedObjectUndo(view.gameObject, "Mount CompletedStoryViewer UI");

        WireRootToPageView(root, view);
        view.EnsureStoryToggleButton();
        EditorUtility.SetDirty(view);
        CreatePrefabIfMissing(view);

        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Selection.activeGameObject = view.gameObject;

        Debug.Log("[StoryBricks] 已将 CompletedStoryViewer UI 挂到场景，可在 Hierarchy 直接编辑。");
    }

    static bool TryWireExistingCanvas(CompletedStoryViewerRoot root)
    {
        var view = root.pageView;
        if (view == null)
            view = Object.FindObjectOfType<CompletedStoryViewerPageView>();

        if (view == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
                return false;

            view = canvas.GetComponent<CompletedStoryViewerPageView>();
            if (view == null)
                view = canvas.gameObject.AddComponent<CompletedStoryViewerPageView>();
        }

        view.WireFromSceneHierarchy();
        if (!view.IsComplete)
            return false;

        WireRootToPageView(root, view);
        view.EnsureStoryToggleButton();
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = view.gameObject;
        return true;
    }

    static void WireRootToPageView(CompletedStoryViewerRoot root, CompletedStoryViewerPageView view)
    {
        root.pageView = view;
        root.allowRuntimeFallbackUi = false;
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(view);
    }

    static void CreatePrefabIfMissing(CompletedStoryViewerPageView view)
    {
        if (view == null || File.Exists(PrefabPath))
            return;

        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Resources/UI");
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            view.gameObject,
            PrefabPath,
            InteractionMode.AutomatedAction);
        SyncPrefabToResources();
        AssetDatabase.SaveAssets();
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
