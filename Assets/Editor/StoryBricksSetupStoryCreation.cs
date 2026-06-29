#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class StoryBricksSetupStoryCreation
{
    const string ScenePath = "Assets/Scenes/StoryCreation.unity";

    [MenuItem("StoryBricks/搭建 StoryCreation 分页创作场景")]
    public static void Setup()
    {
        SetupScene();
        StoryBricksSetupStoryCreationPageUi.BatchWireStoryCreationSceneAndSave();
        StoryBricksBuildSettings.EnsureSceneEnabled(ScenePath);
        UpdateTortoiseHareCreationSceneName();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            "已搭建 StoryCreation：\n" +
            "• 场景：Assets/Scenes/StoryCreation.unity\n" +
            "• 已挂载 StoryCreationCanvas（可在 Hierarchy 可视化编辑）\n" +
            "• 已加入 Build Settings\n" +
            "• 龟兔赛跑 creationSceneName 已设为 StoryCreation\n\n" +
            "流程：StorySummary → StoryPrologue → StoryWorks → 教程 → StoryWorks → StoryCreation",
            "好的");
    }

    static void SetupScene()
    {
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        if (Object.FindObjectOfType<StoryCreationPageBootstrap>() == null)
        {
            var root = new GameObject("StoryCreation");
            root.AddComponent<StoryCreationPageBootstrap>();
        }

        if (!File.Exists(ScenePath))
            EditorSceneManager.SaveScene(scene, ScenePath);
        else
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static void UpdateTortoiseHareCreationSceneName()
    {
        const string assetPath = "Assets/Resources/Stories/Story_TortoiseAndTheHare.asset";
        var def = AssetDatabase.LoadAssetAtPath<StoryDefinition>(assetPath);
        if (def == null)
            return;
        def.creationSceneName = StoryFlowScenes.StoryCreation;
        EditorUtility.SetDirty(def);
    }
}
#endif
