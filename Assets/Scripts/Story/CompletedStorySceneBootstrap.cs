using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>进入「我的故事」相关场景时，确保根组件存在（场景文件里未摆也会自动创建）。</summary>
public static class CompletedStorySceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBootstrap(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBootstrap(scene);

    static void TryBootstrap(Scene scene)
    {
        if (IsLibraryScene(scene) && Object.FindObjectOfType<CompletedStoryLibraryRoot>() == null)
        {
            var go = new GameObject("CompletedStoryLibraryRoot");
            go.AddComponent<CompletedStoryLibraryRoot>();
            Debug.Log("[CompletedStory] 已自动创建 CompletedStoryLibraryRoot。");
        }
        else if (IsViewerScene(scene) && Object.FindObjectOfType<CompletedStoryViewerRoot>() == null)
        {
            var go = new GameObject("CompletedStoryViewerRoot");
            go.AddComponent<CompletedStoryViewerRoot>();
            Debug.Log("[CompletedStory] 已自动创建 CompletedStoryViewerRoot。");
        }
    }

    static bool IsLibraryScene(Scene scene) =>
        scene.name == StoryFlowScenes.CompletedStoryLibrary ||
        scene.path.Contains("CompletedStoryLibrary");

    static bool IsViewerScene(Scene scene) =>
        scene.name == StoryFlowScenes.CompletedStoryViewer ||
        scene.path.Contains("CompletedStoryViewer");
}
