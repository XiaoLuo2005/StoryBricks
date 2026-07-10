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
    [Tooltip("可选：图片标题（如积木库顶栏美术字），与 headerTitle 二选一")]
    public Image headerTitleImage;
    public ScrollRect scrollRect;
    public RectTransform cardListContent;
    public GameObject emptyHint;
    public Button backButton;
    [Tooltip("StoryWorks：左上角「积木库」")]
    public Button brickLibraryButton;
    [Tooltip("StoryWorks：右下角「开始创作故事」")]
    public Button startCreationButton;
    [Tooltip("StoryLibrary：右下角「我的故事」")]
    public Button myStoriesButton;

    public bool IsComplete =>
        canvas != null &&
        scrollRect != null &&
        cardListContent != null;

    /// <summary>从 Hierarchy 补齐引用，便于场景里直接摆 UI 后绑定。</summary>
    public bool WireFromSceneHierarchy(RectTransform listContentOverride = null)
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (listContentOverride != null)
            cardListContent = listContentOverride;
        else if (cardListContent == null && scrollRect != null)
            cardListContent = scrollRect.content;

        if (scrollRect == null && cardListContent != null)
            scrollRect = cardListContent.GetComponentInParent<ScrollRect>();

        if (headerTitle == null && headerTitleImage == null && canvas != null)
        {
            var header = canvas.transform.Find("HeaderTitle");
            if (header != null)
            {
                headerTitle = header.GetComponent<TextMeshProUGUI>();
                headerTitleImage = header.GetComponent<Image>();
            }
        }

        if (backButton == null && canvas != null)
        {
            var back = canvas.transform.Find("BackButton");
            if (back != null)
                backButton = back.GetComponent<Button>();
        }

        if (brickLibraryButton == null && canvas != null)
        {
            var brick = canvas.transform.Find("BrickLibraryButton");
            if (brick != null)
                brickLibraryButton = brick.GetComponent<Button>();
        }

        if (startCreationButton == null && canvas != null)
        {
            var start = canvas.transform.Find("StartCreationButton");
            if (start != null)
                startCreationButton = start.GetComponent<Button>();
        }

        if (myStoriesButton == null && canvas != null)
        {
            var mine = canvas.transform.Find("MyStoriesButton");
            if (mine != null)
                myStoriesButton = mine.GetComponent<Button>();
        }

        return IsComplete;
    }
}
