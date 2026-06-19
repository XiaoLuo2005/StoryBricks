#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class StoryBricksSetupCompletedStories
{
    const string LibraryScenePath = "Assets/Scenes/CompletedStoryLibrary 1.unity";
    const string ViewerScenePath = "Assets/Scenes/CompletedStoryViewer 1.unity";

    [MenuItem("StoryBricks/搭建 我的故事（绘本合集）")]
    public static void Setup()
    {
        EnsureRuntimeResources();
        SetupScene(LibraryScenePath, typeof(CompletedStoryLibraryRoot), "CompletedStoryLibraryRoot");
        SetupScene(ViewerScenePath, typeof(CompletedStoryViewerRoot), "CompletedStoryViewerRoot");
        StoryBricksBuildSettings.EnsureAllFlowScenesEnabled();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            "已搭建「我的故事」场景：\n" +
            "• CompletedStoryLibrary 1（绘本列表，样式同故事库）\n" +
            "• CompletedStoryViewer 1（翻页阅读）\n" +
            "• Resources/UI/StoryCard 与 StorySummary/Background\n" +
            "• 已加入 Build Settings",
            "好的");
    }

    static void EnsureRuntimeResources()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/StorySummary");
        System.IO.Directory.CreateDirectory("Assets/Resources/UI");

        const string backgroundSrc = "Assets/Art/故事积木 (2).png";
        const string backgroundDst = "Assets/Resources/StorySummary/Background.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundDst) == null &&
            AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundSrc) != null)
        {
            AssetDatabase.CopyAsset(backgroundSrc, backgroundDst);
        }

        const string titleSrc = "Assets/Art/故事积木 (2)(1).png";
        const string titleDst = "Assets/Resources/StorySummary/TitleBanner.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(titleDst) == null &&
            AssetDatabase.LoadAssetAtPath<Texture2D>(titleSrc) != null)
        {
            AssetDatabase.CopyAsset(titleSrc, titleDst);
        }

        EnsureSpriteImport(backgroundDst);
        EnsureSpriteImport(titleDst);

        const string cardSrc = "Assets/Prefabs/UI/StoryCard.prefab";
        const string cardDst = "Assets/Resources/UI/StoryCard.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(cardDst) == null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(cardSrc) != null)
        {
            AssetDatabase.CopyAsset(cardSrc, cardDst);
        }

        AssetDatabase.Refresh();
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
        importer.SaveAndReimport();
    }

    static void SetupScene(string scenePath, System.Type rootType, string rootName)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (var obj in Object.FindObjectsOfType<Transform>())
        {
            if (obj == null)
                continue;
            if (obj.GetComponent<Camera>() != null || obj.GetComponent<Light>() != null)
                continue;
            if (obj.GetComponent(rootType) != null)
                continue;
            if (obj.name == "EventSystem" || obj.name.Contains("Canvas"))
                Object.DestroyImmediate(obj.gameObject);
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        if (Object.FindObjectOfType(rootType) == null)
            new GameObject(rootName, rootType);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        StoryBricksBuildSettings.EnsureSceneEnabled(scenePath);
    }
}
#endif
