#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// 统一管理 StoryBricks 流程场景在 Build Settings 中的注册与启用状态。
/// </summary>
public static class StoryBricksBuildSettings
{
    static readonly string[] FlowScenePaths =
    {
        "Assets/Scenes/StartScene.unity",
        "Assets/Scenes/StorySummary.unity",
        "Assets/Scenes/StoryPrologue.unity",
        "Assets/Scenes/StoryWorks.unity",
        "Assets/Scenes/StoryCreation.unity",
        "Assets/Scenes/BrickLibrary.unity",
        "Assets/Scenes/RabbitTutorial.unity",
        "Assets/Scenes/ToitorseTutorial.unity",
        "Assets/Scenes/SnailTutorial.unity",
        "Assets/Scenes/DogTutorial.unity",
    };

    [MenuItem("StoryBricks/修复 Build Settings（启用全部流程场景）")]
    public static void EnsureAllFlowScenesEnabledFromMenu()
    {
        EnsureAllFlowScenesEnabled();
        EditorUtility.DisplayDialog("完成",
            "已启用 Build Settings 中的全部流程场景。\n" +
            "入口场景：StartScene → StorySummary → …",
            "好的");
    }

    public static void EnsureAllFlowScenesEnabled()
    {
        foreach (string path in FlowScenePaths)
            EnsureSceneEnabled(path);
    }

    /// <summary>确保场景在 Build Settings 中且 enabled=true。</summary>
    public static void EnsureSceneEnabled(string scenePath)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = list.FindIndex(s => s.path == scenePath);
        if (index >= 0)
        {
            var existing = list[index];
            if (!existing.enabled)
            {
                list[index] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = list.ToArray();
            }
            return;
        }

        int insertAt = FindInsertIndex(list, scenePath);
        list.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static int FindInsertIndex(List<EditorBuildSettingsScene> list, string scenePath)
    {
        int flowIndex = System.Array.IndexOf(FlowScenePaths, scenePath);
        if (flowIndex < 0)
            return list.Count;

        for (int i = flowIndex - 1; i >= 0; i--)
        {
            int existing = list.FindIndex(s => s.path == FlowScenePaths[i]);
            if (existing >= 0)
                return existing + 1;
        }

        return 0;
    }
}
#endif
