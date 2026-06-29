#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class StoryBricksSetupStoryCreationPageUi
{
    const string PrefabPath = "Assets/Prefabs/UI/StoryCreationPage.prefab";
    const string ResourcesPrefabPath = "Assets/Resources/UI/StoryCreationPage.prefab";
    const string ScenePath = "Assets/Scenes/StoryCreation.unity";

    [MenuItem("StoryBricks/创作页/创建 StoryCreation UI Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Resources/UI");

        var view = StoryCreationPageUiBuilder.BuildPageView(null);
        view.gameObject.SetActive(true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath);
        Object.DestroyImmediate(view.gameObject);

        SyncPrefabToResources();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "创作页 UI Prefab 已创建",
            "已生成：\n" +
            $"• {PrefabPath}\n" +
            $"• {ResourcesPrefabPath}\n\n" +
            "可在 Prefab 模式调整：\n" +
            "• StoryToggleButton / PageCaptionPanel / StoryCloseButton\n" +
            "• 底部按钮 / 摄像头预览 / BackButton",
            "好的");

        Selection.activeObject = prefab;
    }

    public static void BatchRefreshStoryCreationPrefab()
    {
        CreatePrefab();
    }

    public static void BatchWireStoryCreationSceneAndSave()
    {
        if (!File.Exists(ScenePath))
            StoryBricksSetupStoryCreation.Setup();

        CreatePrefab();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (Object.FindObjectOfType<StoryCreationPageBootstrap>() == null)
        {
            var rootGo = new GameObject("StoryCreation");
            rootGo.AddComponent<StoryCreationPageBootstrap>();
        }

        var root = Object.FindObjectOfType<StoryCreationPageBootstrap>();
        WireStoryCreationScene(root, replaceExistingUi: true);
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("StoryBricks/创作页/保留现有布局并挂载")]
    public static void WireExistingScene()
    {
        var root = Object.FindObjectOfType<StoryCreationPageBootstrap>();
        if (root == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 StoryCreationPageBootstrap。", "好的");
            return;
        }

        if (WireStoryCreationScene(root, replaceExistingUi: false))
        {
            EditorUtility.DisplayDialog(
                "已挂载",
                "StoryCreation 已绑定场景 UI。\n\n" +
                "可在 Hierarchy 直接编辑 StoryCreationCanvas 下的\n" +
                "StoryToggleButton、PageCaptionPanel、StoryCloseButton 等。",
                "好的");
            return;
        }

        CreatePrefab();
        if (root.pageView != null && PrefabUtility.IsPartOfPrefabInstance(root.pageView.gameObject))
        {
            PrefabUtility.RevertPrefabInstance(root.pageView.gameObject, InteractionMode.UserAction);
            WireRootToPageView(root, root.pageView, root.pageViewPrefab);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Selection.activeGameObject = root.pageView.gameObject;
            EditorUtility.DisplayDialog(
                "已更新 Prefab 实例",
                "已从最新 Prefab 同步 UI，可直接在 Hierarchy 编辑。",
                "好的");
        }
    }

    [MenuItem("StoryBricks/创作页/当前场景挂载可视化 UI")]
    public static void MountInActiveScene()
    {
        var root = Object.FindObjectOfType<StoryCreationPageBootstrap>();
        if (root == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "当前场景里没有 StoryCreationPageBootstrap。", "好的");
            return;
        }

        WireStoryCreationScene(root, replaceExistingUi: root.pageView == null);
    }

    static bool WireStoryCreationScene(StoryCreationPageBootstrap root, bool replaceExistingUi)
    {
        if (root == null)
            return false;

        if (!replaceExistingUi && TryWireExistingCanvas(root))
            return true;

        var prefab = AssetDatabase.LoadAssetAtPath<StoryCreationPageView>(PrefabPath);
        if (prefab == null)
        {
            CreatePrefab();
            prefab = AssetDatabase.LoadAssetAtPath<StoryCreationPageView>(PrefabPath);
        }

        if (prefab == null)
            return false;

        if (root.pageView != null && root.pageView.gameObject.scene.IsValid())
        {
            if (!replaceExistingUi &&
                !EditorUtility.DisplayDialog("替换现有 UI", "场景里已有 pageView，要删除并重新挂载吗？", "替换", "取消"))
                return false;
            Object.DestroyImmediate(root.pageView.gameObject);
            root.pageView = null;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var instance = (StoryCreationPageView)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "StoryCreationCanvas";
        Undo.RegisterCreatedObjectUndo(instance.gameObject, "Mount StoryCreation UI");

        WireRootToPageView(root, instance, prefab);
        EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
        Selection.activeObject = instance.gameObject;
        Debug.Log("[StoryBricks] 已将 StoryCreation UI 挂到场景，可在 Hierarchy 直接编辑。");
        return true;
    }

    static bool TryWireExistingCanvas(StoryCreationPageBootstrap root)
    {
        var view = root.pageView;
        if (view == null)
            view = Object.FindObjectOfType<StoryCreationPageView>();

        if (view == null)
            return false;

        view.WireFromSceneHierarchy();
        if (!view.IsComplete)
            return false;

        WireRootToPageView(root, view, root.pageViewPrefab);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = view.gameObject;
        return true;
    }

    static void WireRootToPageView(
        StoryCreationPageBootstrap root,
        StoryCreationPageView view,
        StoryCreationPageView prefab)
    {
        root.pageView = view;
        if (prefab != null)
            root.pageViewPrefab = prefab;
        root.allowRuntimeFallbackUi = false;
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(view);
    }

    [MenuItem("StoryBricks/创作页/将当前场景 UI 布局保存到共用 Prefab")]
    public static void ApplySceneLayoutToSharedPrefab()
    {
        var view = Object.FindObjectOfType<StoryCreationPageView>();
        if (view == null)
        {
            EditorUtility.DisplayDialog("未找到 UI", "请先运行「当前场景挂载可视化 UI」。", "好的");
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
        {
            EditorUtility.DisplayDialog("不是 Prefab 实例", "StoryCreationCanvas 必须是 Prefab 实例。", "好的");
            return;
        }

        PrefabUtility.ApplyPrefabInstance(view.gameObject, InteractionMode.UserAction);
        SyncPrefabToResources();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("已保存", "创作页 UI 排版已写入共用 Prefab。", "好的");
    }

    [MenuItem("StoryBricks/创作页/打开 StoryCreation 场景并挂载 UI")]
    public static void OpenSceneAndMount()
    {
        if (!File.Exists(ScenePath))
            StoryBricksSetupStoryCreation.Setup();
        else
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        MountInActiveScene();
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
