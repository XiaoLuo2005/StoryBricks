using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public class StoryProloguePictureBook : MonoBehaviour
{
    const float EdgePadding = 64f;
    const float BottomInset = 56f;
    const float ButtonSpacing = 36f;
    static readonly Vector2 NavButtonSize = new Vector2(200f, 200f);
    static readonly Vector2 StartButtonSize = new Vector2(240f, 200f);

    public string fallbackLibrarySceneName = StoryFlowScenes.StoryLibrary;
    public string backSceneName = StoryFlowScenes.StoryLibrary;
    public bool showBackButton = true;
    public Image pageImage;
    public TextMeshProUGUI pageCaptionTextTmp;
    public TextMeshProUGUI pageIndicatorTextTmp;
    public Button prevPageButton;
    public Button nextPageButton;
    public Button startBuildButton;

    Sprite[] _pages;
    int _index;

    void Awake()
    {
        ApplyResponsiveLayout();
    }

    void ApplyResponsiveLayout()
    {
        var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>() ??
                         canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (pageImage != null)
        {
            StretchFull(pageImage.rectTransform);
            pageImage.preserveAspect = false;
            pageImage.type = Image.Type.Simple;
            pageImage.raycastTarget = false;
        }

        LayoutBottomButton(prevPageButton, EdgePadding, false, NavButtonSize, 30);
        LayoutBottomButton(
            nextPageButton,
            EdgePadding + NavButtonSize.x + ButtonSpacing,
            false,
            NavButtonSize,
            30);
        LayoutBottomButton(startBuildButton, EdgePadding, true, StartButtonSize, 28);

        if (pageIndicatorTextTmp != null)
        {
            var rt = pageIndicatorTextTmp.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(280f, 72f);
            rt.anchoredPosition = new Vector2(
                -(EdgePadding + StartButtonSize.x + ButtonSpacing),
                BottomInset + (NavButtonSize.y - 72f) * 0.5f);
            pageIndicatorTextTmp.fontSize = 42;
            pageIndicatorTextTmp.fontStyle = FontStyles.Bold;
            pageIndicatorTextTmp.color = Color.white;
            pageIndicatorTextTmp.outlineWidth = 0.25f;
            pageIndicatorTextTmp.outlineColor = new Color32(40, 40, 40, 200);
            pageIndicatorTextTmp.alignment = TextAlignmentOptions.MidlineRight;
        }

        BringControlsToFront();
        TryCreateBackButton();
    }

    void TryCreateBackButton()
    {
        if (!showBackButton || string.IsNullOrWhiteSpace(backSceneName))
            return;

        var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        var btn = StoryFlowBackButtonUi.EnsureTopLeft(canvas, "← 返回故事库", backSceneName);
        if (btn != null)
            btn.transform.SetAsLastSibling();
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void LayoutBottomButton(
        Button button,
        float inset,
        bool alignRight,
        Vector2 size,
        float labelFontSize)
    {
        if (button == null)
            return;

        var rt = button.GetComponent<RectTransform>();
        var xAnchor = alignRight ? 1f : 0f;
        rt.anchorMin = new Vector2(xAnchor, 0f);
        rt.anchorMax = new Vector2(xAnchor, 0f);
        rt.pivot = new Vector2(xAnchor, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(alignRight ? -inset : inset, BottomInset);

        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.fontSize = labelFontSize;
            label.enableAutoSizing = false;
        }
    }

    void BringControlsToFront()
    {
        if (pageIndicatorTextTmp != null)
            pageIndicatorTextTmp.transform.SetAsLastSibling();
        if (startBuildButton != null)
            startBuildButton.transform.SetAsLastSibling();
        if (nextPageButton != null)
            nextPageButton.transform.SetAsLastSibling();
        if (prevPageButton != null)
            prevPageButton.transform.SetAsLastSibling();
    }

    void Start()
    {
        if (!StorySelectionContext.HasSelection)
        {
            Debug.LogWarning("StoryPrologue: 无故事上下文，返回故事库。");
            SceneManager.LoadScene(fallbackLibrarySceneName.Trim());
            return;
        }

        _pages = StorySelectionContext.ProloguePages;
        if (_pages == null || _pages.Length == 0)
        {
            if (StorySelectionContext.Cover != null)
                _pages = new[] { StorySelectionContext.Cover };
        }

        _index = 0;
        WireButtons();

        if (_pages == null || _pages.Length == 0)
        {
            Debug.LogWarning(
                $"StoryPrologue: 「{StorySelectionContext.Title}」未配置绘本页，但仍可点击开始搭建。");
            return;
        }

        ShowPage(_index);
    }

    void WireButtons()
    {
        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveAllListeners();
            prevPageButton.onClick.AddListener(PrevPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.AddListener(NextPage);
        }
        if (startBuildButton != null)
        {
            startBuildButton.onClick.RemoveAllListeners();
            startBuildButton.onClick.AddListener(OnStartBuildClicked);
        }
    }

    void ShowPage(int index)
    {
        if (pageImage != null && index >= 0 && index < _pages.Length)
        {
            pageImage.sprite = _pages[index];
            pageImage.preserveAspect = false;
        }

        string indicator = $"{index + 1} / {_pages.Length}";
        if (pageIndicatorTextTmp != null)
            pageIndicatorTextTmp.text = indicator;

        if (prevPageButton != null)
            prevPageButton.interactable = index > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = index < _pages.Length - 1;
    }

    public void PrevPage()
    {
        if (_index <= 0)
            return;
        _index--;
        ShowPage(_index);
    }

    public void NextPage()
    {
        if (_index >= _pages.Length - 1)
            return;
        _index++;
        ShowPage(_index);
    }

    void OnStartBuildClicked()
    {
        if (!StorySelectionContext.HasStoryWorks)
        {
            Debug.LogError(
                $"StoryPrologue: 故事「{StorySelectionContext.Title}」未配置有效的 Works，无法进入 StoryWorks。请在 StoryDefinition 里填写积木作品与 Tutorial Scene Name。");
            return;
        }

        var worksScene = StorySelectionContext.StoryWorksSceneName.Trim();
        Debug.Log($"StoryPrologue: 进入故事作品集 → {worksScene}");
        SceneManager.LoadScene(worksScene);
    }
}
