using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 故事库 / 我的故事 列表页 UI 引用。在 Prefab 或场景里可视化摆放后由 Root 绑定逻辑。
/// </summary>
[DisallowMultipleComponent]
public class StoryLibraryPageView : MonoBehaviour
{
    public Canvas canvas;
    [Tooltip("世界空间背景装饰 StoryLibraryDecor，可在 Scene 视图拖拽")]
    public Transform decorRoot;
    public TextMeshProUGUI headerTitle;
    public ScrollRect scrollRect;
    public RectTransform cardListContent;
    public GameObject emptyHint;
    public Button backButton;

    public bool IsComplete =>
        canvas != null &&
        scrollRect != null &&
        cardListContent != null;
}
