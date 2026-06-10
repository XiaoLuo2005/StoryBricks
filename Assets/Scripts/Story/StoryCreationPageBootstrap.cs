using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 分页故事创作场景：读取 StorySelectionContext.CreationPages，展示背景/引导/摄像头取景区，驱动分页状态机。
/// </summary>
[DefaultExecutionOrder(0)]
[DisallowMultipleComponent]
public class StoryCreationPageBootstrap : MonoBehaviour
{
    enum CreationPhase
    {
        Guide,
        Building,
        Capturing,
        Generating,
        PageDone,
        StoryFinished,
    }

    const float TopBarHeight = 120f;
    const float BottomBarHeight = 168f;
    const float GuidePanelHeight = 120f;
    const float CaptureCountdownSeconds = 3f;
    const float CameraPreviewMiniWidth = 300f;
    const float CameraPreviewMiniHeight = 169f;
    const float CameraPreviewMargin = 24f;

    public string fallbackLibrarySceneName = StoryFlowScenes.StoryLibrary;
    public string backSceneName = StoryFlowScenes.StoryWorks;
    public string finishSceneName = StoryFlowScenes.StoryWorks;

    StoryDefinition.StoryPageDefinition[] _pages;
    int _pageIndex;
    CreationPhase _phase = CreationPhase.Guide;

    Image _backgroundImage;
    RawImage _cameraPreviewMini;
    RawImage _cameraPreviewExpanded;
    GameObject _cameraPreviewOverlay;
    bool _cameraPreviewExpandedOpen;
    Text _pageTitleText;
    Text _pageIndicatorText;
    Text _guideText;
    Text _statusText;
    Button _voiceGuideButton;
    Button _confirmButton;
    Button _rebuildButton;
    Button _nextPageButton;

    static Font BuiltinUIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake()
    {
        if (!StorySelectionContext.HasSelection)
        {
            Debug.LogWarning("StoryCreation: 无故事上下文，返回故事库。");
            SceneManager.LoadScene(fallbackLibrarySceneName.Trim());
            return;
        }

        if (!StorySelectionContext.HasCreationPages)
        {
            Debug.LogWarning(
                $"StoryCreation: 「{StorySelectionContext.Title}」未配置 creationPages，返回故事库。");
            SceneManager.LoadScene(fallbackLibrarySceneName.Trim());
            return;
        }

        _pages = StorySelectionContext.CreationPages;
        if (!StorySessionCache.HasActiveSession ||
            StorySessionCache.StoryId != StorySelectionContext.StoryId)
        {
            StorySessionCache.BeginSession(StorySelectionContext.StoryId, StorySelectionContext.Title);
        }

        _pageIndex = Mathf.Clamp(StorySessionCache.CurrentPageIndex, 0, _pages.Length - 1);
        EnsureEventSystem();
        BuildUi();
        ShowCurrentPage();
        SetPhase(CreationPhase.Building);
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("StoryCreationCanvas", typeof(RectTransform));
        SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        StretchFull(root);

        _backgroundImage = CreateUiObject<Image>(root, "Background");
        StretchFull(_backgroundImage.rectTransform);
        _backgroundImage.color = Color.white;
        _backgroundImage.preserveAspect = false;
        _backgroundImage.raycastTarget = false;

        var topBar = CreateUiObject<Image>(root, "TopBar");
        var topRt = topBar.rectTransform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.sizeDelta = new Vector2(0f, TopBarHeight);
        topRt.anchoredPosition = Vector2.zero;
        topBar.color = new Color32(255, 255, 255, 235);

        StoryFlowBackButtonUi.EnsureTopLeft(canvas, "← 返回作品集", backSceneName);

        var titleGo = CreateUiLabel(topRt, "StoryTitle", StorySelectionContext.Title, 34, TextAnchor.MiddleCenter);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.22f, 0f);
        titleRt.anchorMax = new Vector2(0.78f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        _pageTitleText = CreateUiLabel(topRt, "PageTitle", "", 28, TextAnchor.MiddleRight)
            .GetComponent<Text>();
        var pageTitleRt = _pageTitleText.rectTransform;
        pageTitleRt.anchorMin = new Vector2(0.72f, 0.1f);
        pageTitleRt.anchorMax = new Vector2(0.98f, 0.9f);
        pageTitleRt.offsetMin = Vector2.zero;
        pageTitleRt.offsetMax = Vector2.zero;

        _pageIndicatorText = CreateUiLabel(topRt, "PageIndicator", "", 24, TextAnchor.MiddleRight)
            .GetComponent<Text>();
        var indicatorRt = _pageIndicatorText.rectTransform;
        indicatorRt.anchorMin = new Vector2(0.58f, 0.1f);
        indicatorRt.anchorMax = new Vector2(0.72f, 0.9f);
        indicatorRt.offsetMin = Vector2.zero;
        indicatorRt.offsetMax = Vector2.zero;
        _pageIndicatorText.color = new Color32(90, 96, 110, 255);

        var guidePanel = CreateUiObject<Image>(root, "GuidePanel");
        var guideRt = guidePanel.rectTransform;
        guideRt.anchorMin = new Vector2(0f, 0f);
        guideRt.anchorMax = new Vector2(1f, 0f);
        guideRt.pivot = new Vector2(0.5f, 0f);
        guideRt.sizeDelta = new Vector2(0f, GuidePanelHeight);
        guideRt.anchoredPosition = new Vector2(0f, BottomBarHeight);
        guidePanel.color = new Color32(255, 255, 255, 230);

        _guideText = CreateUiLabel(guidePanel.transform, "GuideText", "", 30, TextAnchor.MiddleLeft)
            .GetComponent<Text>();
        var guideTextRt = _guideText.rectTransform;
        guideTextRt.anchorMin = new Vector2(0.03f, 0.1f);
        guideTextRt.anchorMax = new Vector2(0.97f, 0.9f);
        guideTextRt.offsetMin = Vector2.zero;
        guideTextRt.offsetMax = Vector2.zero;

        var bottomBar = CreateUiObject<Image>(root, "BottomBar");
        var bottomRt = bottomBar.rectTransform;
        bottomRt.anchorMin = new Vector2(0f, 0f);
        bottomRt.anchorMax = new Vector2(1f, 0f);
        bottomRt.pivot = new Vector2(0.5f, 0f);
        bottomRt.sizeDelta = new Vector2(0f, BottomBarHeight);
        bottomRt.anchoredPosition = Vector2.zero;
        bottomBar.color = new Color32(248, 249, 252, 250);

        var bottomLayout = bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        bottomLayout.padding = new RectOffset(32, 32, 20, 20);
        bottomLayout.spacing = 20f;
        bottomLayout.childAlignment = TextAnchor.MiddleCenter;
        bottomLayout.childControlWidth = true;
        bottomLayout.childForceExpandWidth = true;
        bottomLayout.childControlHeight = true;
        bottomLayout.childForceExpandHeight = true;

        _voiceGuideButton = CreateBottomButton(bottomBar.transform, "VoiceGuideButton", "播放引导");
        _confirmButton = CreateBottomButton(bottomBar.transform, "ConfirmButton", "确认生成");
        _rebuildButton = CreateBottomButton(bottomBar.transform, "RebuildButton", "重搭本页");
        _nextPageButton = CreateBottomButton(bottomBar.transform, "NextPageButton", "下一页");

        _voiceGuideButton.onClick.AddListener(OnVoiceGuideClicked);
        _confirmButton.onClick.AddListener(OnConfirmClicked);
        _rebuildButton.onClick.AddListener(OnRebuildClicked);
        _nextPageButton.onClick.AddListener(OnNextPageClicked);

        var statusGo = CreateUiLabel(root, "StatusText", "", 22, TextAnchor.LowerCenter);
        _statusText = statusGo.GetComponent<Text>();
        var statusRt = _statusText.rectTransform;
        statusRt.anchorMin = new Vector2(0.2f, 0f);
        statusRt.anchorMax = new Vector2(0.8f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.sizeDelta = new Vector2(0f, 36f);
        statusRt.anchoredPosition = new Vector2(0f, BottomBarHeight + GuidePanelHeight + 8f);
        _statusText.color = new Color32(70, 120, 200, 255);

        BuildCameraPreviewUi(root);
        var mini = root.Find("CameraPreviewMini");
        if (mini != null)
            mini.SetAsLastSibling();
        if (_cameraPreviewOverlay != null)
            _cameraPreviewOverlay.transform.SetAsLastSibling();
    }

    void BuildCameraPreviewUi(RectTransform root)
    {
        var miniRoot = new GameObject("CameraPreviewMini", typeof(RectTransform));
        miniRoot.layer = LayerMask.NameToLayer("UI");
        var miniRt = miniRoot.GetComponent<RectTransform>();
        miniRt.SetParent(root, false);
        miniRt.anchorMin = new Vector2(1f, 1f);
        miniRt.anchorMax = new Vector2(1f, 1f);
        miniRt.pivot = new Vector2(1f, 1f);
        miniRt.sizeDelta = new Vector2(CameraPreviewMiniWidth, CameraPreviewMiniHeight);
        miniRt.anchoredPosition = new Vector2(-CameraPreviewMargin, -TopBarHeight - CameraPreviewMargin);

        var miniFrame = CreateUiObject<Image>(miniRt, "Frame");
        StretchFull(miniFrame.rectTransform);
        miniFrame.color = new Color32(24, 28, 36, 230);

        _cameraPreviewMini = CreateCameraPreviewRawImage(miniFrame.transform, "Preview");

        var expandBtn = miniFrame.gameObject.AddComponent<Button>();
        expandBtn.targetGraphic = miniFrame;
        expandBtn.onClick.AddListener(() => SetCameraPreviewExpanded(true));

        _cameraPreviewOverlay = new GameObject("CameraPreviewOverlay", typeof(RectTransform));
        _cameraPreviewOverlay.layer = LayerMask.NameToLayer("UI");
        var overlayRt = _cameraPreviewOverlay.GetComponent<RectTransform>();
        overlayRt.SetParent(root, false);
        StretchFull(overlayRt);
        _cameraPreviewOverlay.SetActive(false);

        var backdrop = CreateUiObject<Image>(overlayRt, "Backdrop");
        StretchFull(backdrop.rectTransform);
        backdrop.color = new Color32(0, 0, 0, 170);
        var backdropBtn = backdrop.gameObject.AddComponent<Button>();
        backdropBtn.targetGraphic = backdrop;
        backdropBtn.onClick.AddListener(() => SetCameraPreviewExpanded(false));

        var panel = CreateUiObject<Image>(overlayRt, "ExpandedPanel");
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1280f, 760f);
        panel.color = new Color32(24, 28, 36, 245);

        _cameraPreviewExpanded = CreateCameraPreviewRawImage(panel.transform, "ExpandedPreview");

        var panelBtn = panel.gameObject.AddComponent<Button>();
        panelBtn.targetGraphic = panel;
        panelBtn.onClick.AddListener(() => SetCameraPreviewExpanded(false));
    }

    static RawImage CreateCameraPreviewRawImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        StretchFull(rt);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        var raw = go.AddComponent<RawImage>();
        raw.color = new Color32(36, 40, 48, 255);
        raw.raycastTarget = false;
        return raw;
    }

    void SetCameraPreviewExpanded(bool open)
    {
        _cameraPreviewExpandedOpen = open;
        if (_cameraPreviewOverlay != null)
            _cameraPreviewOverlay.SetActive(open);
        if (open && _cameraPreviewExpanded != null && _cameraPreviewMini != null)
            _cameraPreviewExpanded.texture = _cameraPreviewMini.texture;
    }

    /// <summary>后续接入摄像头时，调用此方法同时更新小窗与放大视图。</summary>
    public void SetCameraPreviewTexture(Texture texture)
    {
        if (_cameraPreviewMini != null)
            _cameraPreviewMini.texture = texture;
        if (_cameraPreviewExpandedOpen && _cameraPreviewExpanded != null)
            _cameraPreviewExpanded.texture = texture;
    }

    void ShowCurrentPage()
    {
        var page = GetCurrentPage();
        if (page == null)
            return;

        StorySessionCache.SetCurrentPageIndex(_pageIndex);

        if (_backgroundImage != null)
        {
            _backgroundImage.sprite = page.backgroundSprite;
            _backgroundImage.enabled = page.backgroundSprite != null;
            if (page.backgroundSprite == null)
                _backgroundImage.color = new Color32(230, 235, 245, 255);
        }

        if (_pageTitleText != null)
            _pageTitleText.text = page.pageTitle ?? "";
        if (_pageIndicatorText != null)
            _pageIndicatorText.text = $"{_pageIndex + 1} / {_pages.Length}";
        if (_guideText != null)
            _guideText.text = page.sceneGuideText ?? "";

        UpdateActionButtons();
        SetStatus("");
    }

    StoryDefinition.StoryPageDefinition GetCurrentPage()
    {
        if (_pages == null || _pageIndex < 0 || _pageIndex >= _pages.Length)
            return null;
        return _pages[_pageIndex];
    }

    void SetPhase(CreationPhase phase)
    {
        _phase = phase;
        UpdateActionButtons();
    }

    void UpdateActionButtons()
    {
        bool busy = _phase == CreationPhase.Capturing || _phase == CreationPhase.Generating;
        bool pageDone = _phase == CreationPhase.PageDone;
        bool lastPage = _pages != null && _pageIndex >= _pages.Length - 1;

        if (_voiceGuideButton != null)
            _voiceGuideButton.interactable = !busy && !pageDone;
        if (_confirmButton != null)
            _confirmButton.interactable = !busy && !pageDone;
        if (_rebuildButton != null)
            _rebuildButton.interactable = !busy;
        if (_nextPageButton != null)
        {
            _nextPageButton.interactable = pageDone;
            var label = _nextPageButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = lastPage ? "完成故事" : "下一页";
        }
    }

    void SetStatus(string text)
    {
        if (_statusText != null)
            _statusText.text = text ?? "";
    }

    void OnVoiceGuideClicked()
    {
        var page = GetCurrentPage();
        if (page == null || string.IsNullOrWhiteSpace(page.voiceGuideText))
        {
            SetStatus("本页未配置语音引导。");
            return;
        }

        Debug.Log($"[StoryCreation][TTS] {page.voiceGuideText}");
        SetStatus("已播放引导语音（当前为日志占位，后续接 TTS）。");
    }

    void OnConfirmClicked()
    {
        if (_phase == CreationPhase.Capturing || _phase == CreationPhase.Generating)
            return;
        StartCoroutine(ConfirmAndGenerateCoroutine());
    }

    IEnumerator ConfirmAndGenerateCoroutine()
    {
        SetPhase(CreationPhase.Capturing);
        for (int i = (int)CaptureCountdownSeconds; i > 0; i--)
        {
            SetStatus($"请移开手部，{i} 秒后抓拍…");
            yield return new WaitForSeconds(1f);
        }

        SetPhase(CreationPhase.Generating);
        SetStatus("正在识别并生成本页故事…");

        // 占位：组员接入识别结果后，在此校验 requiredCharacterIds、触发 AI 提问、调用生图/文案。
        yield return new WaitForSeconds(0.6f);

        var page = GetCurrentPage();
        StorySessionCache.RecordCompletedPage(new StorySessionCache.PageRecord
        {
            pageId = page?.pageId ?? "",
            pageTitle = page?.pageTitle ?? "",
            sceneGuideText = page?.sceneGuideText ?? "",
            voiceGuideText = page?.voiceGuideText ?? "",
            generatedStoryText = $"（占位）{page?.pageTitle} 的故事段落待接入大模型。",
            generatedImageNote = "占位：本页绘本图待接入生图服务。",
        });

        SetPhase(CreationPhase.PageDone);
        SetStatus("本页创作完成，可进入下一页。");
        Debug.Log(
            $"[StoryCreation] 页完成 page={page?.pageId}，历史剧情摘要：\n{StorySessionCache.BuildPreviousPagesSummary()}");
    }

    void OnRebuildClicked()
    {
        StopAllCoroutines();
        SetCameraPreviewExpanded(false);
        SetPhase(CreationPhase.Building);
        SetStatus("已清空本页状态，请重新摆放积木。");
    }

    void OnNextPageClicked()
    {
        if (_phase != CreationPhase.PageDone)
            return;

        bool lastPage = _pageIndex >= _pages.Length - 1;
        if (lastPage)
        {
            SetPhase(CreationPhase.StoryFinished);
            SetStatus("故事创作完成！");
            var scene = ResolveFinishSceneName();
            Debug.Log($"[StoryCreation] 全部完成，进入 {scene}");
            SceneManager.LoadScene(scene);
            return;
        }

        _pageIndex++;
        ShowCurrentPage();
        SetPhase(CreationPhase.Building);
    }

    string ResolveFinishSceneName()
    {
        if (StorySelectionContext.HasStoryWorks)
            return StorySelectionContext.StoryWorksSceneName.Trim();
        return string.IsNullOrWhiteSpace(finishSceneName)
            ? StoryFlowScenes.StoryWorks
            : finishSceneName.Trim();
    }

    static T CreateUiObject<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    static GameObject CreateUiLabel(Transform parent, string name, string text, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = BuiltinUIFont;
        t.fontSize = fontSize;
        t.color = new Color32(40, 44, 52, 255);
        t.text = text;
        t.alignment = align;
        return go;
    }

    static Button CreateBottomButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 88f;
        le.preferredHeight = 88f;

        var img = go.AddComponent<Image>();
        img.color = new Color32(66, 133, 244, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(go.transform, false);
        StretchFull(textRt);
        var text = textGo.AddComponent<Text>();
        text.font = BuiltinUIFont;
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        return btn;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
