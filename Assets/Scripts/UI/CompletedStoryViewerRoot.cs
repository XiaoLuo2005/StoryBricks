using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>翻页浏览已保存的完整故事绘本（前情 + 创作页）。</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class CompletedStoryViewerRoot : MonoBehaviour
{
    public string backSceneName = StoryFlowScenes.CompletedStoryLibrary;
    public string missingSelectionSceneName = StoryFlowScenes.CompletedStoryLibrary;
    public string exitButtonLabel = "← 退出";

    [Header("UI（Prefab / 场景可视化编辑）")]
    public CompletedStoryViewerPageView pageView;
    public CompletedStoryViewerPageView pageViewPrefab;
    public bool allowRuntimeFallbackUi = true;

    Image _pageImage;
    CompletedStoryRuntimeUi.StoryReaderPanelRefs _readerPanel;
    TextMeshProUGUI _captionText;
    Text _indicatorText;
    Button _prevButton;
    Button _nextButton;
    Button _exitButton;
    Button _panoramaToggleButton;
    Text _panoramaHintText;
    GyroPanorama360Player _panoramaPlayer;
    CompletedStoryPageVoiceRecorder _voiceRecorder;
    Button _storyToggleButton;
    Button _storyCloseButton;
    bool _storyPanelVisible;
    bool _panoramaActive;
    const string PanoramaEnterLabel = "360° 全景";
    const string PanoramaExitLabel = "退出全景";
    const string StoryToggleShowLabel = "故事阅读";
    const string StoryToggleHideLabel = "收起故事";

    CompletedStoryStore.CompletedStorySaveFile _save;
    Sprite[] _sprites;
    CompletedStoryStore.CompletedStoryPageFile[] _pages;
    int _index;
    bool _uiBuilt;
    bool _controlsWired;

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

        EnsurePageView();
        if (pageView == null || !pageView.IsComplete)
        {
            Debug.LogError("[CompletedStoryViewer] 未找到可用的 CompletedStoryViewerPageView。");
            return;
        }

        BindFromPageView();
        EnsurePanoramaPlayer();
        WireControls();
        BringControlsToFront();
        _uiBuilt = true;
    }

    void EnsurePageView()
    {
        if (pageView != null && pageView.IsComplete)
            return;

        if (pageView != null)
            pageView.WireFromSceneHierarchy();

        if (pageView != null && pageView.IsComplete)
            return;

        if (pageView == null)
        {
            pageView = FindObjectOfType<CompletedStoryViewerPageView>();
            if (pageView != null)
                pageView.WireFromSceneHierarchy();
        }

        if (pageView != null && pageView.IsComplete)
            return;

        if (!allowRuntimeFallbackUi)
            return;

        if (pageViewPrefab == null)
            pageViewPrefab = Resources.Load<CompletedStoryViewerPageView>("UI/CompletedStoryViewerPage");

        if (pageViewPrefab != null && pageView == null)
        {
            pageView = Instantiate(pageViewPrefab);
            pageView.name = pageViewPrefab.name;
            return;
        }

        Debug.LogWarning(
            "[CompletedStoryViewer] 未配置 pageView，正在运行时临时搭建 UI。" +
            "请运行菜单 StoryBricks/我的故事/阅读场景保留现有布局并挂载。");
        CompletedStoryRuntimeUi.EnsureEventSystem();
        pageView = CompletedStoryViewerUiBuilder.BuildPageView(null);
    }

    void BindFromPageView()
    {
        _pageImage = pageView.pageImage;
        _captionText = pageView.storyText;
        if (_captionText != null)
            StoryPageCaptionArt.ApplyReaderCaptionStyle(_captionText, StoryPageCaptionArt.ResolveFont(null));
        _readerPanel = new CompletedStoryRuntimeUi.StoryReaderPanelRefs
        {
            root = pageView.storyReaderPanelRoot,
            storyText = pageView.storyText,
            recordButton = pageView.recordButton,
            playButton = pageView.playButton,
            rerecordButton = pageView.rerecordButton,
            statusText = pageView.voiceStatusText,
            closeButton = pageView.storyCloseButton,
        };
        _prevButton = pageView.prevPageButton;
        _nextButton = pageView.nextPageButton;
        _indicatorText = pageView.pageIndicatorText;
        _exitButton = pageView.exitButton;
        _panoramaToggleButton = pageView.panoramaToggleButton ?? pageView.vrToggleButton;
        _panoramaHintText = pageView.panoramaHintText ?? pageView.vrHintText;
        _storyToggleButton = pageView.storyToggleButton;
        _storyCloseButton = pageView.storyCloseButton ?? _readerPanel?.closeButton;

        if (_exitButton != null)
            StoryFlowBackButtonUi.BindNavigation(_exitButton, exitButtonLabel, backSceneName);

        HideLegacyStereoButton();
        EnsureStoryToggleButton();
        RefreshStoryReaderUi();
    }

    void HideLegacyStereoButton()
    {
        if (pageView.stereoToggleButton != null)
            pageView.stereoToggleButton.gameObject.SetActive(false);
    }

    void EnsureStoryToggleButton()
    {
        if (pageView == null)
            return;

        pageView.EnsureStoryToggleButton();
        _storyToggleButton = pageView.storyToggleButton;
        _storyCloseButton = pageView.storyCloseButton;
        if (_storyCloseButton == null && pageView.storyReaderPanelRoot != null)
        {
            var close = pageView.storyReaderPanelRoot.Find("StoryCloseButton");
            if (close != null)
            {
                _storyCloseButton = close.GetComponent<Button>();
                pageView.storyCloseButton = _storyCloseButton;
            }
        }

        WireStoryToggleClick();
        WireStoryCloseClick();
    }

    void WireStoryCloseClick()
    {
        if (_storyCloseButton == null)
            return;

        _storyCloseButton.onClick.RemoveAllListeners();
        _storyCloseButton.onClick.AddListener(CollapseStoryPanel);
    }

    void WireStoryToggleClick()
    {
        if (_storyToggleButton == null)
            return;

        _storyToggleButton.onClick.RemoveAllListeners();
        _storyToggleButton.onClick.AddListener(ToggleStoryPanel);
    }

    void EnsurePanoramaPlayer()
    {
        _panoramaPlayer = GetComponent<GyroPanorama360Player>();
        if (_panoramaPlayer == null)
            _panoramaPlayer = gameObject.AddComponent<GyroPanorama360Player>();
    }

    void WireControls()
    {
        if (_controlsWired)
            return;

        _voiceRecorder = GetComponent<CompletedStoryPageVoiceRecorder>();
        if (_voiceRecorder == null)
            _voiceRecorder = gameObject.AddComponent<CompletedStoryPageVoiceRecorder>();

        if (_readerPanel?.recordButton != null)
        {
            _readerPanel.recordButton.onClick.RemoveAllListeners();
            _readerPanel.recordButton.onClick.AddListener(() => _voiceRecorder.OnRecordClicked());
        }

        if (_readerPanel?.playButton != null)
        {
            _readerPanel.playButton.onClick.RemoveAllListeners();
            _readerPanel.playButton.onClick.AddListener(() => _voiceRecorder.OnPlayClicked());
        }

        if (_readerPanel?.rerecordButton != null)
        {
            _readerPanel.rerecordButton.onClick.RemoveAllListeners();
            _readerPanel.rerecordButton.onClick.AddListener(() => _voiceRecorder.OnRerecordClicked());
        }

        if (_prevButton != null)
        {
            _prevButton.onClick.RemoveAllListeners();
            _prevButton.onClick.AddListener(PrevPage);
        }

        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.onClick.AddListener(NextPage);
        }

        if (_panoramaToggleButton != null)
        {
            _panoramaToggleButton.onClick.RemoveAllListeners();
            _panoramaToggleButton.onClick.AddListener(TogglePanoramaMode);
        }

        if (_storyCloseButton == null && _readerPanel?.root != null)
        {
            var close = _readerPanel.root.Find("StoryCloseButton");
            if (close != null)
                _storyCloseButton = close.GetComponent<Button>();
        }

        WireStoryToggleClick();
        WireStoryCloseClick();

        _controlsWired = true;
    }

    void ToggleStoryPanel()
    {
        if (_captionText == null || string.IsNullOrWhiteSpace(_captionText.text))
            return;

        _storyPanelVisible = !_storyPanelVisible;
        RefreshStoryReaderUi();
    }

    void CollapseStoryPanel()
    {
        if (!_storyPanelVisible)
            return;

        _storyPanelVisible = false;
        RefreshStoryReaderUi();
    }

    void RefreshStoryReaderUi()
    {
        bool hasCaption = _captionText != null && !string.IsNullOrWhiteSpace(_captionText.text);
        bool flatVisible = _pageImage == null || _pageImage.enabled;
        bool useToggle = _storyToggleButton != null;

        if (_storyToggleButton != null)
        {
            _storyToggleButton.gameObject.SetActive(flatVisible);
            _storyToggleButton.interactable = hasCaption;
            CompletedStoryRuntimeUi.ApplyStoryToggleLayout(_storyToggleButton.GetComponent<RectTransform>());
            CompletedStoryRuntimeUi.SetStoryToggleLabel(
                _storyToggleButton,
                _storyPanelVisible ? StoryToggleHideLabel : StoryToggleShowLabel);
        }

        if (_readerPanel?.root != null)
        {
            bool showPanel = hasCaption && flatVisible && (!useToggle || _storyPanelVisible);
            _readerPanel.root.gameObject.SetActive(showPanel);
        }

        BringStoryToggleToFront();
    }

    void BringStoryToggleToFront()
    {
        if (_readerPanel?.root != null && _readerPanel.root.gameObject.activeSelf)
            _readerPanel.root.SetAsLastSibling();
        if (_storyToggleButton != null)
            _storyToggleButton.transform.SetAsLastSibling();
    }

    void BringControlsToFront()
    {
        BringStoryToggleToFront();
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
        if (_panoramaToggleButton != null)
            _panoramaToggleButton.transform.SetAsLastSibling();
        if (_panoramaHintText != null)
            _panoramaHintText.transform.SetAsLastSibling();
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
            CompletedStoryRuntimeUi.ResetStoryTextScroll(_captionText);
        }

        _storyPanelVisible = false;
        RefreshStoryReaderUi();

        if (_voiceRecorder != null && _save != null)
        {
            _voiceRecorder.Bind(
                _save.saveId,
                _index,
                _pages,
                _readerPanel.recordButton,
                _readerPanel.playButton,
                _readerPanel.rerecordButton,
                _readerPanel.statusText);
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

        if (_panoramaActive)
        {
            if (CurrentPageHasPanorama())
            {
                ApplyPanoramaToPlayer(page);
                UpdatePanoramaHint();
            }
            else
            {
                ExitPanoramaMode();
                ShowTransientHint("本页暂无 360° 全景");
            }
        }

        RefreshPanoramaButton();
    }

    void ApplyPanoramaToPlayer(CompletedStoryStore.CompletedStoryPageFile page)
    {
        if (_panoramaPlayer == null || _save == null || page == null)
            return;

        string path = CompletedStoryStore.GetPagePanoramaPath(_save.saveId, page);
        _panoramaPlayer.SetPageSource(null, path);
    }

    bool CurrentPageHasPanorama()
    {
        if (_save == null || _pages == null || _index < 0 || _index >= _pages.Length)
            return false;
        return !string.IsNullOrWhiteSpace(
            CompletedStoryStore.GetPagePanoramaPath(_save.saveId, _pages[_index]));
    }

    void ExitPanoramaMode()
    {
        if (_panoramaPlayer != null && _panoramaPlayer.IsActive)
            _panoramaPlayer.Exit();
        _panoramaActive = false;
        SetFlatViewVisible(true);
        RefreshPanoramaButton();
        UpdatePanoramaHint();
    }

    void EnterPanoramaMode()
    {
        if (!CurrentPageHasPanorama())
        {
            ShowTransientHint("本页暂无 360° 全景");
            return;
        }

        var page = _pages != null && _index < _pages.Length ? _pages[_index] : null;
        _panoramaActive = true;
        _panoramaPlayer.Enter();
        ApplyPanoramaToPlayer(page);
        SetFlatViewVisible(false);
        RefreshPanoramaButton();
        UpdatePanoramaHint();
    }

    static string GetPageCaption(CompletedStoryStore.CompletedStoryPageFile page)
    {
        if (page == null)
            return "";
        if (!string.IsNullOrWhiteSpace(page.generatedStoryText))
            return page.generatedStoryText.Trim();
        return page.userVoiceAnswer?.Trim() ?? "";
    }

    void TogglePanoramaMode()
    {
        if (_panoramaActive)
            ExitPanoramaMode();
        else
            EnterPanoramaMode();
    }

    void RefreshPanoramaButton()
    {
        if (_panoramaToggleButton == null)
            return;

        bool hasPanorama = CurrentPageHasPanorama();
        _panoramaToggleButton.interactable = hasPanorama || _panoramaActive;

        var label = _panoramaToggleButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = _panoramaActive ? PanoramaExitLabel : PanoramaEnterLabel;
    }

    void SetFlatViewVisible(bool visible)
    {
        if (_pageImage != null)
            _pageImage.enabled = visible;

        if (!visible)
            _storyPanelVisible = false;

        RefreshStoryReaderUi();
    }

    void UpdatePanoramaHint()
    {
        if (_panoramaHintText == null)
            return;

        if (!_panoramaActive)
        {
            _panoramaHintText.gameObject.SetActive(false);
            return;
        }

        _panoramaHintText.gameObject.SetActive(true);
        string lookHint = SystemInfo.supportsGyroscope
            ? "转动设备环视"
            : "按住鼠标拖拽环视";
        _panoramaHintText.text = $"{lookHint} · 360° 全景";
    }

    void ShowTransientHint(string message)
    {
        if (_panoramaHintText == null || string.IsNullOrWhiteSpace(message))
            return;

        _panoramaHintText.gameObject.SetActive(true);
        _panoramaHintText.text = message;
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
