using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>翻页浏览已保存的完整故事绘本（前情 + 创作页）。</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class CompletedStoryViewerRoot : MonoBehaviour
{
    const float EdgePadding = 64f;
    const float BottomInset = 56f;
    const float ButtonSpacing = 36f;
    static readonly Vector2 NavButtonSize = new Vector2(200f, 200f);

    public string backSceneName = StoryFlowScenes.CompletedStoryLibrary;
    public string missingSelectionSceneName = StoryFlowScenes.CompletedStoryLibrary;
    public string exitButtonLabel = "← 退出";

    Image _pageImage;
    Text _captionText;
    Text _indicatorText;
    Button _prevButton;
    Button _nextButton;
    Button _exitButton;
    Button _vrToggleButton;
    Button _stereoToggleButton;
    Text _vrHintText;
    MobileVrStoryTheater _vrTheater;

    CompletedStoryStore.CompletedStorySaveFile _save;
    Sprite[] _sprites;
    CompletedStoryStore.CompletedStoryPageFile[] _pages;
    int _index;
    Font _font;
    bool _uiBuilt;

    void Awake()
    {
        BuildUiIfNeeded();
        TryLoadStory();
    }

    void Start()
    {
        BuildUiIfNeeded();
        TryLoadStory();
    }

    void BuildUiIfNeeded()
    {
        if (_uiBuilt)
            return;

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CompletedStoryRuntimeUi.EnsureEventSystem();

        var canvas = CompletedStoryRuntimeUi.CreateOverlayCanvas("CompletedStoryViewerCanvas");
        CompletedStoryRuntimeUi.EnsureCanvasScaler(canvas);

        _pageImage = CompletedStoryRuntimeUi.CreateFullScreenImage(canvas.transform, "PageImage");
        _captionText = CompletedStoryRuntimeUi.CreateBottomCaption(canvas.transform, _font);
        (_prevButton, _nextButton, _indicatorText) =
            CompletedStoryRuntimeUi.CreateBottomNav(canvas.transform, _font, NavButtonSize, EdgePadding, BottomInset, ButtonSpacing);

        _prevButton.onClick.AddListener(PrevPage);
        _nextButton.onClick.AddListener(NextPage);

        _exitButton = StoryFlowBackButtonUi.EnsureTopLeft(canvas, exitButtonLabel, backSceneName);

        _vrTheater = GetComponent<MobileVrStoryTheater>();
        if (_vrTheater == null)
            _vrTheater = gameObject.AddComponent<MobileVrStoryTheater>();

        _vrToggleButton = CreateTopBarButton(canvas.transform, "VrToggleButton", "沉浸 VR", 0);
        _vrToggleButton.onClick.AddListener(ToggleVrMode);

        _stereoToggleButton = CreateTopBarButton(canvas.transform, "StereoToggleButton", "立体分屏", 1);
        _stereoToggleButton.gameObject.SetActive(false);
        _stereoToggleButton.onClick.AddListener(ToggleStereoMode);

        _vrHintText = CreateVrHint(canvas.transform);

        BringControlsToFront();

        _uiBuilt = true;
    }

    void BringControlsToFront()
    {
        if (_captionText != null)
            _captionText.transform.SetAsLastSibling();
        if (_indicatorText != null)
            _indicatorText.transform.SetAsLastSibling();
        if (_prevButton != null)
            _prevButton.transform.SetAsLastSibling();
        if (_nextButton != null)
            _nextButton.transform.SetAsLastSibling();
        if (_exitButton != null)
            _exitButton.transform.SetAsLastSibling();
        if (_vrToggleButton != null)
            _vrToggleButton.transform.SetAsLastSibling();
        if (_stereoToggleButton != null)
            _stereoToggleButton.transform.SetAsLastSibling();
        if (_vrHintText != null)
            _vrHintText.transform.SetAsLastSibling();
    }

    void TryLoadStory()
    {
        if (_save != null)
            return;

        if (!CompletedStoryContext.HasSelection)
        {
            Debug.LogWarning("[CompletedStoryViewer] 未选择绘本，返回列表。");
            SceneManager.LoadScene(missingSelectionSceneName);
            return;
        }

        _save = CompletedStoryStore.LoadSave(CompletedStoryContext.SelectedSaveId);
        if (_save == null || _save.pages == null || _save.pages.Length == 0)
        {
            Debug.LogWarning("[CompletedStoryViewer] 绘本数据缺失，返回列表。");
            SceneManager.LoadScene(missingSelectionSceneName);
            return;
        }

        LoadSprites();
        _index = 0;
        ShowPage(_index);
    }

    void LoadSprites()
    {
        _pages = _save.pages;
        _sprites = new Sprite[_pages.Length];
        string saveId = _save.saveId;
        for (int i = 0; i < _pages.Length; i++)
            _sprites[i] = CompletedStoryStore.LoadPageSprite(saveId, _pages[i]);
    }

    void ShowPage(int index)
    {
        if (_pages == null || _pages.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, _pages.Length - 1);
        _index = index;

        var page = _pages[index];
        if (_pageImage != null)
        {
            _pageImage.sprite = _sprites != null && index < _sprites.Length ? _sprites[index] : null;
            _pageImage.color = _pageImage.sprite != null ? Color.white : new Color32(30, 34, 42, 255);
        }

        if (_captionText != null)
        {
            string caption = GetPageCaption(page);
            _captionText.text = caption;
            _captionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(caption));
        }

        if (_indicatorText != null)
        {
            string kind = page.isPrologue ? "前情" : "创作";
            _indicatorText.text = $"{index + 1}/{_pages.Length} · {page.pageTitle}（{kind}）";
        }

        if (_prevButton != null)
            _prevButton.interactable = index > 0;
        if (_nextButton != null)
            _nextButton.interactable = index < _pages.Length - 1;

        if (_vrTheater != null && _vrTheater.IsActive)
            _vrTheater.SetPage(_sprites != null && index < _sprites.Length ? _sprites[index] : null, GetPageCaption(page));
    }

    static string GetPageCaption(CompletedStoryStore.CompletedStoryPageFile page)
    {
        if (page == null)
            return "";
        return !string.IsNullOrWhiteSpace(page.userVoiceAnswer)
            ? page.userVoiceAnswer.Trim()
            : page.generatedStoryText?.Trim() ?? "";
    }

    void ToggleVrMode()
    {
        if (_vrTheater == null)
            return;

        if (_vrTheater.IsActive)
        {
            _vrTheater.Exit();
            SetFlatViewVisible(true);
            if (_stereoToggleButton != null)
                _stereoToggleButton.gameObject.SetActive(false);
            if (_vrToggleButton != null)
            {
                var label = _vrToggleButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "沉浸 VR";
            }
        }
        else
        {
            _vrTheater.Enter();
            var page = _pages != null && _index < _pages.Length ? _pages[_index] : null;
            _vrTheater.SetPage(
                _sprites != null && _index < _sprites.Length ? _sprites[_index] : null,
                GetPageCaption(page));
            SetFlatViewVisible(false);
            if (_stereoToggleButton != null)
                _stereoToggleButton.gameObject.SetActive(true);
            if (_vrToggleButton != null)
            {
                var label = _vrToggleButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "退出 VR";
            }
        }

        UpdateVrHint();
    }

    void ToggleStereoMode()
    {
        if (_vrTheater == null || !_vrTheater.IsActive)
            return;

        _vrTheater.SetStereoEnabled(!_vrTheater.StereoEnabled);
        UpdateVrHint();
        if (_stereoToggleButton != null)
        {
            var label = _stereoToggleButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _vrTheater.StereoEnabled ? "单屏" : "立体分屏";
        }
    }

    void SetFlatViewVisible(bool visible)
    {
        if (_pageImage != null)
            _pageImage.enabled = visible;
        if (_captionText != null && visible)
            _captionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(_captionText.text));
        else if (_captionText != null)
            _captionText.gameObject.SetActive(false);
    }

    void UpdateVrHint()
    {
        if (_vrHintText == null)
            return;

        if (_vrTheater == null || !_vrTheater.IsActive)
        {
            _vrHintText.gameObject.SetActive(false);
            return;
        }

        _vrHintText.gameObject.SetActive(true);
        string lookHint = SystemInfo.supportsGyroscope
            ? "转动设备环视"
            : "按住鼠标拖拽环视";
        string stereoHint = _vrTheater.StereoEnabled ? " · 立体分屏已开" : "";
        _vrHintText.text = $"{lookHint}{stereoHint}";
    }

    static Button CreateTopBarButton(Transform parent, string name, string label, int columnIndex)
    {
        const float width = 200f;
        const float height = 72f;
        const float margin = 28f;
        const float spacing = 16f;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(-margin - columnIndex * (width + spacing), -margin);

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(40, 44, 52, 255);
        text.text = label;
        return button;
    }

    static Text CreateVrHint(Transform parent)
    {
        var go = new GameObject("VrHint", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, 56f);
        rt.anchoredPosition = new Vector2(0f, -108f);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(255, 255, 255, 230);
        text.text = "";
        go.SetActive(false);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 160);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    void PrevPage()
    {
        if (_index <= 0)
            return;
        ShowPage(_index - 1);
    }

    void NextPage()
    {
        if (_index >= _pages.Length - 1)
            return;
        ShowPage(_index + 1);
    }
}
