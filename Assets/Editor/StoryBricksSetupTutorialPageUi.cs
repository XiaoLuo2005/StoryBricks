#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 生成可可视化编辑的 TutorialStepsPage Prefab，并挂到教程场景。
/// </summary>
public static class StoryBricksSetupTutorialPageUi
{
    const string PrefabPath = "Assets/Prefabs/UI/TutorialStepsPage.prefab";
    const string ResourcesPrefabPath = "Assets/Resources/UI/TutorialStepsPage.prefab";

    [MenuItem("StoryBricks/教程/同步 Tutorial UI 美术资源")]
    public static void SyncArtResources()
    {
        EnsureTutorialUiResources();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "Tutorial UI 美术",
            "已同步到 Assets/Resources/TutorialUi/\n" +
            "• Background（故事积木 2）\n" +
            "• Button（故事积木 1 AI看图）\n" +
            "• LelePanelBackground（StoryCard）\n" +
            "• word SDF 字体",
            "好的");
    }

    [MenuItem("StoryBricks/教程/将当前场景 UI 布局保存到共用 Prefab（复用到其他教程）")]
    public static void ApplySceneLayoutToSharedPrefab()
    {
        var view = Object.FindObjectOfType<TutorialStepsPageView>();
        if (view == null)
        {
            EditorUtility.DisplayDialog(
                "未找到 UI",
                "当前场景里没有 TutorialCanvas。\n" +
                "请先打开已调好 UI 的教程场景（如 ToitorseTutorial），或运行「当前场景挂载 Tutorial UI」。",
                "好的");
            return;
        }

        var root = view.gameObject;
        if (!PrefabUtility.IsPartOfPrefabInstance(root))
        {
            EditorUtility.DisplayDialog(
                "不是 Prefab 实例",
                "Hierarchy 里的 TutorialCanvas 必须是 TutorialStepsPage Prefab 的实例，\n" +
                "这样「保存布局」才会写回共用 Prefab，供兔子/狗/蜗牛等场景复用。",
                "好的");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "保存 UI 布局",
                "将把当前场景里 TutorialCanvas 的位置/大小等改动，\n" +
                "写入共用 Prefab（所有教程共用）。\n\n" +
                "• 不会改各教程的 TutorialStepsConfig（步骤图、标题等）\n" +
                "• 不会删除乌龟场景，只是把排版「定稿」到 Prefab\n\n" +
                "继续？",
                "保存",
                "取消"))
            return;

        PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
        SyncPrefabToResources();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "已保存",
            "UI 排版已写入：\n" +
            $"• {PrefabPath}\n" +
            $"• {ResourcesPrefabPath}\n\n" +
            "兔子 / 狗 / 蜗牛等教程 Play 时会自动加载同一套 UI。\n" +
            "各教程仍使用各自的 TutorialStepsConfig 内容。",
            "好的");

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    [MenuItem("StoryBricks/教程/修复 Prefab 布局（解锁 Pos，可拖拽）")]
    public static void FixPrefabLayout()
    {
        if (!EditorUtility.DisplayDialog(
                "注意",
                "此操作会用代码默认布局重建底栏/乐乐面板，\n" +
                "会覆盖你在 Prefab 或场景里手调的位置。\n\n" +
                "若只是要把乌龟场景的布局复用到其他教程，\n" +
                "请用「将当前场景 UI 布局保存到共用 Prefab」。\n\n" +
                "仍要继续修复？",
                "继续重建",
                "取消"))
            return;

        EnsureTutorialUiResources();

        if (!System.IO.File.Exists(PrefabPath))
        {
            CreatePrefab();
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var view = scope.prefabContentsRoot.GetComponent<TutorialStepsPageView>();
            if (view == null)
            {
                EditorUtility.DisplayDialog("失败", "Prefab 根节点缺少 TutorialStepsPageView。", "好的");
                return;
            }

            RemoveAllLayoutDrivers(scope.prefabContentsRoot);

            var root = scope.prefabContentsRoot.GetComponent<RectTransform>();
            TutorialStepsPageUiBuilder.ReplaceBottomControls(root, view);
            TutorialStepsPageUiBuilder.EnsureStepLabel(root, view);

            var leleRoot = root.Find("LelePanelRoot") as RectTransform;
            if (leleRoot != null)
                view.lelePanel = TutorialLelePanelUiBuilder.Build(leleRoot);

            if (view.stepViewer != null)
            {
                view.stepViewer.stepText = view.stepLabelText;
                view.stepViewer.progressBar = view.progressSlider;
                view.stepViewer.prevButton = view.prevButton;
                view.stepViewer.nextButton = view.nextButton;
            }
        }

        SyncPrefabToResources();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "已修复",
            "已移除 LayoutGroup / LayoutElement，并重建 BottomControls。\n\n" +
            "现在可在 Prefab 模式自由改 Pos：\n" +
            "• StepViewer / StepLabel\n" +
            "• BottomControls / ProgressSlider / PrevButton / NextButton\n" +
            "• LelePanelRoot（整体）及其子节点 Title / DialogScroll / ListenStatus / Status",
            "好的");
    }

    static void RemoveAllLayoutDrivers(GameObject root)
    {
        foreach (var lg in root.GetComponentsInChildren<UnityEngine.UI.HorizontalLayoutGroup>(true))
            Object.DestroyImmediate(lg);
        foreach (var lg in root.GetComponentsInChildren<UnityEngine.UI.VerticalLayoutGroup>(true))
            Object.DestroyImmediate(lg);
        foreach (var le in root.GetComponentsInChildren<UnityEngine.UI.LayoutElement>(true))
            Object.DestroyImmediate(le);
    }

    [MenuItem("StoryBricks/教程/创建 TutorialStepsPage UI Prefab")]
    public static void CreatePrefab()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog(
                "会覆盖现有 Prefab",
                "此操作会从代码重新生成 TutorialStepsPage Prefab，\n" +
                "会丢失你在 Prefab / 场景里手调的 UI 位置。\n\n" +
                "若已有调好的乌龟教程 UI，请用\n" +
                "「将当前场景 UI 布局保存到共用 Prefab」。\n\n" +
                "仍要重新生成？",
                "重新生成",
                "取消"))
            return;

        EnsureTutorialUiResources();
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Resources/UI");

        var view = TutorialStepsPageUiBuilder.Build(null, "TutorialStepsPage");
        view.gameObject.SetActive(true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath);
        Object.DestroyImmediate(view.gameObject);

        SyncPrefabToResources();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Tutorial UI Prefab 已创建",
            "已生成：\n" +
            $"• {PrefabPath}\n" +
            $"• {ResourcesPrefabPath}\n\n" +
            "双击 Prefab 进入 Prefab 模式，可拖拽这些节点调位置：\n" +
            "• StepViewer（步骤图区域）\n" +
            "• BottomControls（进度条 + 翻页按钮）\n" +
            "• LelePanelRoot（乐乐整体）\n" +
            "  └ Title / DialogScroll / ListenStatus / Status\n" +
            "• MascotRoot（左下角吉祥物）\n" +
            "• TopBar 下的 BackButton / Title\n\n" +
            "Play 时乐乐只绑定逻辑，不再运行时重建 UI。",
            "好的");

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    [MenuItem("StoryBricks/教程/当前场景挂载 Tutorial UI")]
    public static void MountInActiveScene()
    {
        var bootstrap = Object.FindObjectOfType<TutorialStepsPageBootstrap>();
        if (bootstrap == null)
        {
            EditorUtility.DisplayDialog("未找到 Bootstrap", "当前场景里没有 TutorialStepsPageBootstrap。", "好的");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<TutorialStepsPageView>(PrefabPath);
        if (prefab == null)
        {
            CreatePrefab();
            prefab = AssetDatabase.LoadAssetAtPath<TutorialStepsPageView>(PrefabPath);
        }

        if (prefab == null)
            return;

        if (bootstrap.pageView != null && bootstrap.pageView.gameObject.scene.IsValid())
        {
            if (!EditorUtility.DisplayDialog(
                    "替换现有 UI",
                    "场景里已有 pageView，要删除并重新挂载吗？",
                    "替换",
                    "取消"))
                return;

            Object.DestroyImmediate(bootstrap.pageView.gameObject);
            bootstrap.pageView = null;
        }

        var instance = (TutorialStepsPageView)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "TutorialCanvas";
        Undo.RegisterCreatedObjectUndo(instance.gameObject, "Mount Tutorial UI");

        bootstrap.pageView = instance;
        bootstrap.pageViewPrefab = prefab;
        bootstrap.allowRuntimeFallbackUi = false;
        EditorUtility.SetDirty(bootstrap);

        EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
        Selection.activeGameObject = instance.gameObject;

        Debug.Log("[StoryBricks] 已将 TutorialStepsPage 挂到场景，可在 Hierarchy 选中 TutorialCanvas 编辑。");
    }

    [MenuItem("StoryBricks/教程/打开 TutorialStepsPage Prefab")]
    public static void OpenPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            CreatePrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        if (prefab != null)
            AssetDatabase.OpenAsset(prefab);
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

    public static void EnsureTutorialUiResources()
    {
        EnsureFolder("Assets/Resources/TutorialUi");

        CopyIfMissing("Assets/Art/故事积木 (2).png", "Assets/Resources/TutorialUi/Background.png");
        CopyIfMissing("Assets/Art/故事积木 (1)_AI看图.png", "Assets/Resources/TutorialUi/Button.png");
        CopyIfMissing("Assets/Art/StoryCard_Background.png", "Assets/Resources/TutorialUi/LelePanelBackground.png");

        const string fontSrc = "Assets/Art/word SDF.asset";
        const string fontDst = "Assets/Resources/TutorialUi/word SDF.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(fontDst) == null &&
            AssetDatabase.LoadAssetAtPath<Object>(fontSrc) != null)
        {
            AssetDatabase.CopyAsset(fontSrc, fontDst);
        }

        EnsureSpriteImport("Assets/Resources/TutorialUi/Background.png");
        EnsureSpriteImport("Assets/Resources/TutorialUi/Button.png");
        EnsureSpriteImport("Assets/Resources/TutorialUi/LelePanelBackground.png");

        AssetDatabase.Refresh();
    }

    static void CopyIfMissing(string src, string dst)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(dst) != null)
            return;
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(src) == null)
        {
            Debug.LogWarning($"[StoryBricks] 找不到 Tutorial UI 源图：{src}");
            return;
        }

        AssetDatabase.CopyAsset(src, dst);
    }

    static void EnsureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        if (assetPath.Contains("Button"))
        {
            importer.spriteBorder = new Vector4(40f, 40f, 40f, 40f);
        }

        importer.SaveAndReimport();
    }
}
#endif
