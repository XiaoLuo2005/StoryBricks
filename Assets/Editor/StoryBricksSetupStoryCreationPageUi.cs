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
            "• GuideText / 底部按钮 / 摄像头预览 / 语音回答区\n" +
            "• BackButton / StatusPanel",
            "好的");

        Selection.activeObject = prefab;
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

        var prefab = AssetDatabase.LoadAssetAtPath<StoryCreationPageView>(PrefabPath);
        if (prefab == null)
        {
            CreatePrefab();
            prefab = AssetDatabase.LoadAssetAtPath<StoryCreationPageView>(PrefabPath);
        }

        if (prefab == null)
            return;

        if (root.pageView != null && root.pageView.gameObject.scene.IsValid())
        {
            if (!EditorUtility.DisplayDialog("替换现有 UI", "场景里已有 pageView，要删除并重新挂载吗？", "替换", "取消"))
                return;
            Object.DestroyImmediate(root.pageView.gameObject);
            root.pageView = null;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var instance = (StoryCreationPageView)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "StoryCreationCanvas";
        Undo.RegisterCreatedObjectUndo(instance.gameObject, "Mount StoryCreation UI");

        root.pageView = instance;
        root.pageViewPrefab = prefab;
        root.allowRuntimeFallbackUi = false;
        EditorUtility.SetDirty(root);

        EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
        Selection.activeGameObject = instance.gameObject;

        Debug.Log("[StoryBricks] 已将 StoryCreation UI 挂到场景，可在 Hierarchy 直接编辑。");
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
        {
            StoryBricksSetupStoryCreation.Setup();
        }
        else
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

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
