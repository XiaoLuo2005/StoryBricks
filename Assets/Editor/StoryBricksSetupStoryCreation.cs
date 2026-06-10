#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        EnsureInBuildSettings(ScenePath);
        UpdateTortoiseHareCreationSceneName();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            "已搭建 StoryCreation：\n" +
            "• 场景：Assets/Scenes/StoryCreation.unity\n" +
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

    static void EnsureInBuildSettings(string scenePath)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Any(s => s.path == scenePath))
            return;

        int insertAt = list.FindIndex(s => s.path.Contains("StoryPrologue"));
        if (insertAt < 0)
            insertAt = list.Count;
        else
            insertAt += 1;

        list.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
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
