#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StoryBricksSetupStoryPrologueUi
{
    const string ScenePath = "Assets/Scenes/StoryPrologue.unity";

    [MenuItem("StoryBricks/绘本前言/添加返回按钮并挂载")]
    public static void AddBackButtonAndWire()
    {
        var book = Object.FindObjectOfType<StoryProloguePictureBook>();
        if (book == null)
        {
            EditorUtility.DisplayDialog("未找到 Root", "场景里没有 PrologueController / StoryProloguePictureBook。", "好的");
            return;
        }

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("未找到 Canvas", "请先在场景里摆放 Canvas。", "好的");
            return;
        }

        var existing = canvas.transform.Find("BackButton");
        if (existing != null)
            book.backButton = existing.GetComponent<Button>();
        else
            book.backButton = StoryLibraryUiBuilder.CreateBackButton(canvas.transform);

        book.showBackButton = true;
        book.WireFromSceneHierarchy();
        EditorUtility.SetDirty(book);
        EditorSceneManager.MarkSceneDirty(book.gameObject.scene);
        Selection.activeGameObject = book.backButton.gameObject;

        EditorUtility.DisplayDialog(
            "已添加",
            "BackButton 已加入 Canvas，并绑定到 PrologueController。\n可在 Hierarchy 里直接调整位置与样式。",
            "好的");
    }

    [MenuItem("StoryBricks/绘本前言/打开 StoryPrologue 并添加返回按钮")]
    public static void OpenSceneAndAddBackButton()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AddBackButtonAndWire();
    }
}
#endif
