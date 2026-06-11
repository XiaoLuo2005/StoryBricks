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

    const float EdgePadding = 48f;
    const float BottomInset = 56f;
    const float ButtonSpacing = 24f;
    const float FloatingButtonHeight = 72f;
    static readonly Vector2 ActionButtonSize = new Vector2(160f, 72f);
    static readonly Vector2 PrimaryButtonSize = new Vector2(200f, 80f);
    const float CaptureCountdownSeconds = 3f;
    const float CameraPreviewMiniWidth = 220f;
    const float CameraPreviewMiniHeight = 124f;
    const float CameraPreviewMargin = 28f;

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

        StoryFlowBackButtonUi.EnsureTopLeft(canvas, "← 返回作品集", backSceneName);

        _pageIndicatorText = CreateOverlayText(root, "PageIndicator", "", 36, TextAnchor.LowerRight);
        var indicatorRt = _pageIndicatorText.rectTransform;
        indicatorRt.anchorMin = new Vector2(1f, 0f);
        indicatorRt.anchorMax = new Vector2(1f, 0f);
        indicatorRt.pivot = new Vector2(1f, 0f);
        indicatorRt.sizeDelta = new Vector2(420f, 64f);
        indicatorRt.anchoredPosition = new Vector2(
            -EdgePadding,
            BottomInset + FloatingButtonHeight + 20f);

        _guideText = CreateOverlayText(root, "GuideText", "", 32, TextAnchor.LowerLeft);
        var guideRt = _guideText.rectTransform;
        guideRt.anchorMin = new Vector2(0f, 0f);
        guideRt.anchorMax = new Vector2(0.62f, 0f);
        guideRt.pivot = new Vector2(0f, 0f);
        guideRt.sizeDelta = new Vector2(0f, 120f);
        guideRt.anchoredPosition = new Vector2(EdgePadding, BottomInset + FloatingButtonHeight + 12f);

        _voiceGuideButton = CreateFloatingButton(root, "VoiceGuideButton", "播放引导", ActionButtonSize, false);
        _rebuildButton = CreateFloatingButton(root, "RebuildButton", "重搭本页", ActionButtonSize, false);
        _confirmButton = CreateFloatingButton(root, "ConfirmButton", "确认生成", PrimaryButtonSize, true);
        _nextPageButton = CreateFloatingButton(root, "NextPageButton", "下一页", ActionButtonSize, false);

        LayoutFloatingButton(_voiceGuideButton, EdgePadding, false, ActionButtonSize);
        LayoutFloatingButton(_rebuildButton, EdgePadding + ActionButtonSize.x + ButtonSpacing, false, ActionButtonSize);
        LayoutFloatingButton(_confirmButton, EdgePadding, true, PrimaryButtonSize);
        LayoutFloatingButton(_nextPageButton, EdgePadding + PrimaryButtonSize.x + ButtonSpacing, true, ActionButtonSize);

        _voiceGuideButton.onClick.AddListener(OnVoiceGuideClicked);
        _confirmButton.onClick.AddListener(OnConfirmClicked);
        _rebuildButton.onClick.AddListener(OnRebuildClicked);
        _nextPageButton.onClick.AddListener(OnNextPageClicked);

        _statusText = CreateOverlayText(root, "StatusText", "", 26, TextAnchor.LowerCenter);
        var statusRt = _statusText.rectTransform;
        statusRt.anchorMin = new Vector2(0.25f, 0f);
        statusRt.anchorMax = new Vector2(0.75f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.sizeDelta = new Vector2(0f, 48f);
        statusRt.anchoredPosition = new Vector2(0f, BottomInset + FloatingButtonHeight + 72f);

        BuildCameraPreviewUi(root);
        _backgroundImage.transform.SetAsFirstSibling();
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
        miniRt.anchoredPosition = new Vector2(-CameraPreviewMargin, -CameraPreviewMargin);

        var miniFrame = CreateUiObject<Image>(miniRt, "Frame");
        StretchFull(miniFrame.rectTransform);
        miniFrame.color = new Color32(0, 0, 0, 140);

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
        panel.color = new Color32(0, 0, 0, 200);

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

        if (_pageIndicatorText != null)
        {
            string title = page.pageTitle ?? "";
            _pageIndicatorText.text = string.IsNullOrEmpty(title)
                ? $"{_pageIndex + 1} / {_pages.Length}"
                : $"{_pageIndex + 1} / {_pages.Length}  ·  {title}";
        }
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
        if (_statusText == null)
            return;
        _statusText.text = text ?? "";
        _statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(_statusText.text));
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

    static Text CreateOverlayText(Transform parent, string name, string text, int fontSize, TextAnchor align)
    {
        var go = CreateUiLabel(parent, name, text, fontSize, align);
        var t = go.GetComponent<Text>();
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 200);
        outline.effectDistance = new Vector2(2f, -2f);
        t.raycastTarget = false;
        return t;
    }

    static Button CreateFloatingButton(
        Transform parent,
        string name,
        string label,
        Vector2 size,
        bool primary)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = primary
            ? new Color32(255, 255, 255, 235)
            : new Color32(0, 0, 0, 120);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(go.transform, false);
        StretchFull(textRt);
        var text = textGo.AddComponent<Text>();
        text.font = BuiltinUIFont;
        text.fontSize = primary ? 30 : 26;
        text.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = primary ? new Color32(40, 44, 52, 255) : Color.white;
        text.text = label;

        if (!primary)
        {
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 160);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        go.GetComponent<RectTransform>().sizeDelta = size;
        return btn;
    }

    static void LayoutFloatingButton(Button button, float inset, bool alignRight, Vector2 size)
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
