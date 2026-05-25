using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 封面「开始」按钮：在 Inspector 填目标场景名，Button OnClick 绑定 <see cref="LoadNextScene"/>。
/// </summary>
public class StartSceneLoadButton : MonoBehaviour
{
    [Tooltip("Build Settings 中的场景名（不含路径），默认 StorySummary")]
    public string nextSceneName = StoryFlowScenes.StoryLibrary;

    public void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("StartSceneLoadButton: nextSceneName 为空。");
            return;
        }

        SceneManager.LoadScene(nextSceneName.Trim());
    }
}
