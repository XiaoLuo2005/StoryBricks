using System.Collections;
using System.Collections.Generic;
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
        VoiceInteracting,
        Generating,
        PageDone,
        StoryFinished,
    }

    const float EdgePadding = 48f;
    const float BottomInset = 56f;
    const float TopStatusBarOffset = 108f;
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

    [Header("生图")]
    [Tooltip("本机调试填 http://127.0.0.1:8800/generate；云服务器需 nginx 放宽 body 限制")]
    public string imageGenServerUrl = "http://127.0.0.1:8800/generate";

    [Header("语音交互")]
    [Tooltip("storybricks-tutor-gateway 地址，用于 TTS 提问与 ASR 收录")]
    public string tutorGatewayUrl = "http://127.0.0.1:8787";

    [Tooltip("可选：在场景中自行摆放后拖入；留空则运行时自动创建「按住说话」按钮")]
    public Button answerVoiceButton;

    [Tooltip("可选：专门显示当前语音提问全文；留空则用左下角 GuideText")]
    public Text voiceQuestionText;

    [Tooltip("是否用大模型（DeepSeek）生成行为类提问；元素问仍用 optionalElementQuestion")]
    public bool useAiGeneratedQuestions = true;

    [Tooltip("生图前是否用大模型整理各来源文本为连贯场景描述；失败则回退本地拼接")]
    public bool useAiPromptRefinement = true;

    [Tooltip("允许在语音问答时切换为文字输入（宿舍测试用）")]
    public bool allowTextAnswerInput = true;

    [Tooltip("进入语音问答时的默认回答方式")]
    public AnswerInputMode defaultAnswerInputMode = AnswerInputMode.Text;

    [Tooltip("可选：文字输入框；留空则运行时自动创建")]
    public InputField answerTextInput;

    [Tooltip("可选：文字提交按钮")]
    public Button answerTextSubmitButton;

    [Tooltip("可选：切换到语音回答")]
    public Button answerModeVoiceButton;

    [Tooltip("可选：切换到文字回答")]
    public Button answerModeTextButton;

    public enum AnswerInputMode
    {
        Voice,
        Text,
    }

    StoryDefinition.StoryPageDefinition[] _pages;
    StoryDefinition.CharacterReferenceEntry[] _characterReferences;
    StoryMarkerTaxonomy _markerTaxonomy = StoryMarkerTaxonomy.Default;
    string _stylePromptPrefix;
    int _pageIndex;
    CreationPhase _phase = CreationPhase.Guide;

    Image _backgroundImage;
    RawImage _generatedPageImage;
    Texture2D _currentGeneratedTexture;
    RawImage _cameraPreviewMini;
    RawImage _cameraPreviewExpanded;
    GameObject _cameraPreviewOverlay;
    bool _cameraPreviewExpandedOpen;
    Text _pageIndicatorText;
    Text _guideText;
    GameObject _statusPanel;
    Text _statusText;
    Button _voiceGuideButton;
    Button _confirmButton;
    Button _rebuildButton;
    Button _nextPageButton;
    Button _runtimeAnswerVoiceButton;
    GameObject _answerUiRoot;
    GameObject _answerVoicePanel;
    GameObject _answerTextPanel;
    AnswerInputMode _answerInputMode = AnswerInputMode.Voice;
    ArUcoDetector _arUcoDetector;
    LocalImageGenClient _imageGenClient;
    StoryCreationVoiceGateway _voiceGateway;

    bool _waitingForVoiceAnswer;
    string _pendingVoiceTranscript;
    string _pendingVoiceError;
    string _currentVoiceQuestion;
    string _currentGapKind;
    string _currentGapRoleName;

    /// <summary>创作页摄像头与 ArUco 检测器，供后续识别/生图流程读取。</summary>
    public ArUcoDetector CameraDetector => _arUcoDetector;

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
        _characterReferences = StorySelectionContext.CharacterReferences;
        _markerTaxonomy = StorySelectionContext.MarkerTaxonomy;
        _stylePromptPrefix = StorySelectionContext.StylePromptPrefix;
        if (!StorySessionCache.HasActiveSession ||
            StorySessionCache.StoryId != StorySelectionContext.StoryId)
        {
            StorySessionCache.BeginSession(StorySelectionContext.StoryId, StorySelectionContext.Title);
        }

        _pageIndex = Mathf.Clamp(StorySessionCache.CurrentPageIndex, 0, _pages.Length - 1);
        EnsureEventSystem();
        BuildUi();
        SetupCameraDetector();
        SetupImageGeneration();
        SetupVoiceGateway();
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

        _generatedPageImage = CreateUiObject<RawImage>(root, "GeneratedPage");
        StretchFull(_generatedPageImage.rectTransform);
        _generatedPageImage.color = Color.white;
        _generatedPageImage.raycastTarget = false;
        _generatedPageImage.gameObject.SetActive(false);

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

        EnsureAnswerVoiceButton(root);
        EnsureAnswerInputUi(root);

        _statusPanel = new GameObject("StatusPanel", typeof(RectTransform));
        _statusPanel.layer = LayerMask.NameToLayer("UI");
        var statusPanelRt = _statusPanel.GetComponent<RectTransform>();
        statusPanelRt.SetParent(root, false);
        statusPanelRt.anchorMin = new Vector2(0.18f, 1f);
        statusPanelRt.anchorMax = new Vector2(0.82f, 1f);
        statusPanelRt.pivot = new Vector2(0.5f, 1f);
        statusPanelRt.sizeDelta = new Vector2(0f, 52f);
        statusPanelRt.anchoredPosition = new Vector2(0f, -TopStatusBarOffset);

        var statusBg = _statusPanel.AddComponent<Image>();
        statusBg.color = new Color32(20, 24, 32, 170);
        statusBg.raycastTarget = false;

        _statusText = CreateOverlayText(_statusPanel.transform, "StatusText", "", 26, TextAnchor.MiddleCenter);
        StretchFull(_statusText.rectTransform);
        _statusText.alignment = TextAnchor.MiddleCenter;
        _statusPanel.SetActive(false);

        BuildCameraPreviewUi(root);
        _backgroundImage.transform.SetAsFirstSibling();
        if (_statusPanel != null)
            _statusPanel.transform.SetAsLastSibling();
        if (_cameraPreviewOverlay != null)
            _cameraPreviewOverlay.transform.SetAsLastSibling();
    }

    void SetupCameraDetector()
    {
        if (_cameraPreviewMini == null)
            return;

        _arUcoDetector = GetComponent<ArUcoDetector>();
        if (_arUcoDetector == null)
            _arUcoDetector = gameObject.AddComponent<ArUcoDetector>();

        _arUcoDetector.displayImage = _cameraPreviewMini;
        _cameraPreviewMini.color = Color.white;
        if (_cameraPreviewExpanded != null)
            _cameraPreviewExpanded.color = Color.white;
    }

    void SetupImageGeneration()
    {
        _imageGenClient = GetComponent<LocalImageGenClient>();
        if (_imageGenClient == null)
            _imageGenClient = gameObject.AddComponent<LocalImageGenClient>();

        if (_generatedPageImage != null)
            _imageGenClient.targetImage = _generatedPageImage;

        if (!string.IsNullOrWhiteSpace(imageGenServerUrl))
            _imageGenClient.serverUrl = imageGenServerUrl.Trim();
    }

    void SetupVoiceGateway()
    {
        _voiceGateway = GetComponent<StoryCreationVoiceGateway>();
        if (_voiceGateway == null)
            _voiceGateway = gameObject.AddComponent<StoryCreationVoiceGateway>();
        if (!string.IsNullOrWhiteSpace(tutorGatewayUrl))
            _voiceGateway.GatewayBaseUrl = tutorGatewayUrl.Trim();
    }

    void EnsureAnswerVoiceButton(RectTransform root)
    {
        if (answerVoiceButton != null)
        {
            WireAnswerVoiceHoldEvents(answerVoiceButton);
            return;
        }

        _runtimeAnswerVoiceButton = CreateFloatingButton(
            root,
            "AnswerVoiceButton",
            "按住说话",
            PrimaryButtonSize,
            true);
        LayoutFloatingButton(_runtimeAnswerVoiceButton, EdgePadding + PrimaryButtonSize.x + ButtonSpacing, true, PrimaryButtonSize);
        WireAnswerVoiceHoldEvents(_runtimeAnswerVoiceButton);
        _runtimeAnswerVoiceButton.gameObject.SetActive(false);
    }

    void WireAnswerVoiceHoldEvents(Button button)
    {
        if (button == null)
            return;

        var trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => OnAnswerVoiceDown(button));
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => OnAnswerVoiceUp(button));
        trigger.triggers.Add(up);
    }

    void EnsureAnswerInputUi(RectTransform root)
    {
        if (!allowTextAnswerInput)
            return;

        if (_answerUiRoot != null)
            return;

        _answerUiRoot = new GameObject("AnswerInputRoot", typeof(RectTransform));
        _answerUiRoot.layer = LayerMask.NameToLayer("UI");
        var rootRt = _answerUiRoot.GetComponent<RectTransform>();
        rootRt.SetParent(root, false);
        rootRt.anchorMin = new Vector2(1f, 0f);
        rootRt.anchorMax = new Vector2(1f, 0f);
        rootRt.pivot = new Vector2(1f, 0f);
        rootRt.sizeDelta = new Vector2(520f, 200f);
        rootRt.anchoredPosition = new Vector2(-EdgePadding, BottomInset + PrimaryButtonSize.y + ButtonSpacing + 8f);
        _answerUiRoot.SetActive(false);

        if (answerModeVoiceButton == null || answerModeTextButton == null)
        {
            answerModeVoiceButton = CreateFloatingButton(
                rootRt,
                "AnswerModeVoice",
                "语音",
                new Vector2(100f, 48f),
                false);
            LayoutChildButton(answerModeVoiceButton, 0f, 152f, new Vector2(100f, 48f));

            answerModeTextButton = CreateFloatingButton(
                rootRt,
                "AnswerModeText",
                "文字",
                new Vector2(100f, 48f),
                false);
            LayoutChildButton(answerModeTextButton, 108f, 152f, new Vector2(100f, 48f));
        }

        answerModeVoiceButton.onClick.AddListener(() => SetAnswerInputMode(AnswerInputMode.Voice));
        answerModeTextButton.onClick.AddListener(() => SetAnswerInputMode(AnswerInputMode.Text));

        _answerVoicePanel = new GameObject("AnswerVoicePanel", typeof(RectTransform));
        _answerVoicePanel.layer = LayerMask.NameToLayer("UI");
        var voicePanelRt = _answerVoicePanel.GetComponent<RectTransform>();
        voicePanelRt.SetParent(rootRt, false);
        StretchFull(voicePanelRt);

        if (answerVoiceButton == null && _runtimeAnswerVoiceButton != null)
        {
            _runtimeAnswerVoiceButton.transform.SetParent(voicePanelRt, false);
            var vBtnRt = _runtimeAnswerVoiceButton.GetComponent<RectTransform>();
            vBtnRt.anchorMin = new Vector2(1f, 0f);
            vBtnRt.anchorMax = new Vector2(1f, 0f);
            vBtnRt.pivot = new Vector2(1f, 0f);
            vBtnRt.anchoredPosition = Vector2.zero;
        }

        _answerTextPanel = new GameObject("AnswerTextPanel", typeof(RectTransform));
        _answerTextPanel.layer = LayerMask.NameToLayer("UI");
        var textPanelRt = _answerTextPanel.GetComponent<RectTransform>();
        textPanelRt.SetParent(rootRt, false);
        StretchFull(textPanelRt);

        if (answerTextInput == null)
        {
            var inputGo = new GameObject("AnswerTextInput", typeof(RectTransform));
            inputGo.layer = LayerMask.NameToLayer("UI");
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.SetParent(textPanelRt, false);
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(1f, 0f);
            inputRt.sizeDelta = new Vector2(-120f, 56f);
            inputRt.anchoredPosition = new Vector2(0f, 72f);

            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color32(255, 255, 255, 230);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = LayerMask.NameToLayer("UI");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(inputGo.transform, false);
            StretchFull(textRt);
            textRt.offsetMin = new Vector2(12f, 8f);
            textRt.offsetMax = new Vector2(-12f, -8f);
            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.layer = LayerMask.NameToLayer("UI");
            var phRt = placeholderGo.GetComponent<RectTransform>();
            phRt.SetParent(inputGo.transform, false);
            StretchFull(phRt);
            phRt.offsetMin = new Vector2(14f, 8f);
            phRt.offsetMax = new Vector2(-12f, -8f);
            var phText = placeholderGo.AddComponent<Text>();
            phText.font = BuiltinUIFont;
            phText.fontSize = 24;
            phText.color = new Color32(120, 124, 132, 200);
            phText.text = "输入回答（测试用）";
            phText.supportRichText = false;

            var inputText = textGo.AddComponent<Text>();
            inputText.font = BuiltinUIFont;
            inputText.fontSize = 24;
            inputText.color = new Color32(40, 44, 52, 255);
            inputText.supportRichText = false;

            answerTextInput = inputGo.AddComponent<InputField>();
            answerTextInput.textComponent = inputText;
            answerTextInput.placeholder = phText;
            answerTextInput.lineType = InputField.LineType.SingleLine;
            answerTextInput.onEndEdit.AddListener(OnAnswerTextEndEdit);
        }

        if (answerTextSubmitButton == null)
        {
            answerTextSubmitButton = CreateFloatingButton(
                textPanelRt,
                "AnswerTextSubmit",
                "提交",
                new Vector2(108f, 56f),
                true);
            var submitRt = answerTextSubmitButton.GetComponent<RectTransform>();
            submitRt.anchorMin = new Vector2(1f, 0f);
            submitRt.anchorMax = new Vector2(1f, 0f);
            submitRt.pivot = new Vector2(1f, 0f);
            submitRt.anchoredPosition = new Vector2(0f, 72f);
        }

        answerTextSubmitButton.onClick.AddListener(OnAnswerTextSubmit);

        SetAnswerInputMode(defaultAnswerInputMode);
    }

    static void LayoutChildButton(Button button, float x, float y, Vector2 size)
    {
        if (button == null)
            return;
        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(x, y);
    }

    void SetAnswerInputMode(AnswerInputMode mode)
    {
        _answerInputMode = mode;
        bool voice = mode == AnswerInputMode.Voice;
        if (_answerVoicePanel != null)
            _answerVoicePanel.SetActive(voice);
        if (_answerTextPanel != null)
            _answerTextPanel.SetActive(!voice);

        var voiceBtn = ActiveAnswerVoiceButton;
        if (voiceBtn != null)
            voiceBtn.gameObject.SetActive(voice && _phase == CreationPhase.VoiceInteracting);

        HighlightModeButton(answerModeVoiceButton, voice);
        HighlightModeButton(answerModeTextButton, !voice);

        if (!voice && answerTextInput != null)
            answerTextInput.ActivateInputField();
    }

    static void HighlightModeButton(Button button, bool active)
    {
        if (button == null)
            return;
        var img = button.GetComponent<Image>();
        if (img != null)
            img.color = active
                ? new Color32(52, 168, 83, 220)
                : new Color32(0, 0, 0, 120);
    }

    void OnAnswerTextSubmit()
    {
        if (_phase != CreationPhase.VoiceInteracting)
            return;

        var text = answerTextInput != null ? answerTextInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("请先输入回答文字");
            return;
        }

        SubmitPendingAnswer(text);
        if (answerTextInput != null)
            answerTextInput.text = "";
    }

    void OnAnswerTextEndEdit(string text)
    {
        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
            return;
        OnAnswerTextSubmit();
    }

    void SubmitPendingAnswer(string text)
    {
        if (!_waitingForVoiceAnswer)
        {
            SetStatus("请等提问出现在屏幕上后再提交");
            return;
        }
        _voiceGateway?.StopPlayback();
        _pendingVoiceTranscript = text.Trim();
        _pendingVoiceError = "";
        _waitingForVoiceAnswer = false;
        SetStatus("收到！准备下一问…");
    }

    string BuildAnswerStatusHint(int index, int total)
    {
        if (allowTextAnswerInput && _answerInputMode == AnswerInputMode.Text)
            return $"提问 {index + 1}/{total} · 文字模式：输入后点「提交」";
        return $"提问 {index + 1}/{total} · 语音模式：按住「说话」回答";
    }

    void UpdateAnswerInputUi()
    {
        bool voicePhase = _phase == CreationPhase.VoiceInteracting;
        bool showAnswerUi = voicePhase && allowTextAnswerInput;

        if (_answerUiRoot != null)
            _answerUiRoot.SetActive(showAnswerUi);

        var voiceBtn = ActiveAnswerVoiceButton;
        if (voiceBtn != null)
        {
            bool showHold = voicePhase && (!allowTextAnswerInput || _answerInputMode == AnswerInputMode.Voice);
            voiceBtn.gameObject.SetActive(showHold);
        }

        if (showAnswerUi)
            SetAnswerInputMode(_answerInputMode);
    }

    Button ActiveAnswerVoiceButton =>
        answerVoiceButton != null ? answerVoiceButton : _runtimeAnswerVoiceButton;

    void LateUpdate()
    {
        if (!_cameraPreviewExpandedOpen || _cameraPreviewExpanded == null || _cameraPreviewMini == null)
            return;
        if (_cameraPreviewExpanded.texture != _cameraPreviewMini.texture)
            _cameraPreviewExpanded.texture = _cameraPreviewMini.texture;
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
        panel.color = new Color32(255, 255, 255, 24);

        _cameraPreviewExpanded = CreateCameraPreviewRawImage(panel.transform, "ExpandedPreview");
        _cameraPreviewExpanded.color = Color.white;

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
        raw.color = Color.white;
        raw.raycastTarget = false;
        return raw;
    }

    void SetCameraPreviewExpanded(bool open)
    {
        _cameraPreviewExpandedOpen = open;
        if (_cameraPreviewOverlay != null)
            _cameraPreviewOverlay.SetActive(open);
        if (open && _cameraPreviewExpanded != null && _cameraPreviewMini != null)
        {
            _cameraPreviewExpanded.texture = _cameraPreviewMini.texture;
            _cameraPreviewExpanded.color = Color.white;
        }
    }

    /// <summary>手动替换预览纹理（默认由 ArUcoDetector 驱动小窗）。</summary>
    public void SetCameraPreviewTexture(Texture texture)
    {
        if (_cameraPreviewMini != null)
        {
            _cameraPreviewMini.texture = texture;
            _cameraPreviewMini.color = texture != null ? Color.white : new Color32(36, 40, 48, 255);
        }
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

        ClearGeneratedPageOverlay();
        UpdateActionButtons();
        SetStatus("");
    }

    void ClearGeneratedPageOverlay()
    {
        if (_generatedPageImage != null)
        {
            _generatedPageImage.texture = null;
            _generatedPageImage.gameObject.SetActive(false);
        }

        if (_currentGeneratedTexture != null)
        {
            Destroy(_currentGeneratedTexture);
            _currentGeneratedTexture = null;
        }
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
        bool busy = _phase == CreationPhase.Capturing ||
                    _phase == CreationPhase.Generating;
        bool voicePhase = _phase == CreationPhase.VoiceInteracting;
        bool pageDone = _phase == CreationPhase.PageDone;
        bool lastPage = _pages != null && _pageIndex >= _pages.Length - 1;

        if (_voiceGuideButton != null)
            _voiceGuideButton.interactable = !busy && !pageDone && !voicePhase;
        if (_confirmButton != null)
        {
            _confirmButton.interactable = !busy && !pageDone && !voicePhase;
            _confirmButton.gameObject.SetActive(!voicePhase);
        }
        if (_rebuildButton != null)
            _rebuildButton.interactable = !busy && !voicePhase;
        if (_nextPageButton != null)
        {
            _nextPageButton.interactable = pageDone;
            var label = _nextPageButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = lastPage ? "完成故事" : "下一页";
        }

        var answerBtn = ActiveAnswerVoiceButton;
        if (answerBtn != null && (!allowTextAnswerInput || _answerInputMode == AnswerInputMode.Voice))
            answerBtn.gameObject.SetActive(voicePhase);

        UpdateAnswerInputUi();
    }

    void SetStatus(string text)
    {
        if (_statusText == null)
            return;
        _statusText.text = text ?? "";
        bool visible = !string.IsNullOrWhiteSpace(_statusText.text);
        if (_statusPanel != null)
            _statusPanel.SetActive(visible);
        else
            _statusText.gameObject.SetActive(visible);
    }

    void OnVoiceGuideClicked()
    {
        var page = GetCurrentPage();
        if (page == null || string.IsNullOrWhiteSpace(page.voiceGuideText))
        {
            SetStatus("本页未配置语音引导。");
            return;
        }

        StartCoroutine(PlayGuideVoiceCoroutine(page.voiceGuideText));
    }

    IEnumerator PlayGuideVoiceCoroutine(string text)
    {
        if (_voiceGateway == null)
        {
            SetStatus("语音服务未就绪。");
            yield break;
        }

        SetStatus("正在播放引导语音…");
        bool ok = false;
        string ttsError = "";
        yield return _voiceGateway.SpeakText(text, (success, err) =>
        {
            ok = success;
            ttsError = err;
        });
        SetStatus(ok ? "" : $"引导语音失败：{ttsError}（请检查 gateway 与 .env 卡密）");
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

        SetStatus("正在识别积木…");

        var page = GetCurrentPage();
        var detectedIds = StoryPageGenerationPipeline.CollectDetectedMarkerIds(_arUcoDetector);
        var validation = StoryPageGenerationPipeline.ValidateRequiredCharacters(page, detectedIds);
        if (!validation.ok)
        {
            SetPhase(CreationPhase.Building);
            SetStatus(validation.message);
            yield break;
        }

        if (_characterReferences == null || _characterReferences.Length == 0)
        {
            SetPhase(CreationPhase.Building);
            SetStatus("故事未配置 characterReferences 角色参考图。");
            yield break;
        }

        // 不把 P1 成图作为 img2img 参考：整页锚图会让模型复制第一页构图，导致各页画面几乎一样。
        var references = StoryPageGenerationPipeline.CollectCharacterReferenceTextures(
            validation.detectedIds,
            _characterReferences,
            anchorTexture: null);

        if (references.characterCount == 0)
        {
            StoryPageGenerationPipeline.ReleaseTemporaryTextures(references);
            SetPhase(CreationPhase.Building);
            SetStatus("未找到已识别角色的参考图，请检查 characterReferences 配置。");
            yield break;
        }

        var markers = _arUcoDetector?.DetectedMarkers;
        var gaps = StoryCreationGapAnalyzer.Analyze(
            page,
            markers,
            _characterReferences,
            _markerTaxonomy);

        string voiceSupplement = "";
        if (gaps.Count > 0)
        {
            SetPhase(CreationPhase.VoiceInteracting);
            yield return RunVoiceInteractionCoroutine(page, gaps, result => voiceSupplement = result ?? "");
            if (_phase == CreationPhase.Building)
                yield break;
        }

        SetPhase(CreationPhase.Generating);
        SetStatus("正在整理生图描述…");

        var promptInputs = StoryPageGenerationPipeline.CollectPromptInputs(
            page,
            _characterReferences,
            validation.detectedIds,
            _stylePromptPrefix,
            references,
            voiceSupplement);

        string refinedScene = null;
        if (useAiPromptRefinement && _voiceGateway != null)
        {
            string refineError = "";
            yield return _voiceGateway.RefineImagePrompt(
                ToPromptRefineRequest(promptInputs),
                (text, err) =>
                {
                    refinedScene = text;
                    refineError = err;
                });
            if (string.IsNullOrWhiteSpace(refinedScene) && !string.IsNullOrEmpty(refineError))
                Debug.LogWarning($"[StoryCreation] AI Prompt 整理失败，使用本地拼接：{refineError}");
        }

        string prompt = StoryPageGenerationPipeline.AssembleFinalPrompt(promptInputs, refinedScene);
        SetStatus("正在生成本页绘本…");

        Debug.Log($"[StoryCreation] 生图 Prompt（最终）：\n{prompt}");

        var outcome = new LocalImageGenClient.GenerateOutcome();
        yield return _imageGenClient.GenerateImageAndWait(prompt, references.textures, outcome);
        StoryPageGenerationPipeline.ReleaseTemporaryTextures(references);

        if (!outcome.success)
        {
            SetPhase(CreationPhase.Building);
            SetStatus("生图失败，请重试。");
            Debug.LogError($"[StoryCreation] 生图失败: {outcome.errorMessage}");
            yield break;
        }

        if (_generatedPageImage != null && outcome.texture != null)
        {
            _currentGeneratedTexture = outcome.texture;
            _generatedPageImage.texture = outcome.texture;
            _generatedPageImage.gameObject.SetActive(true);
        }

        if (_pageIndex == 0 && outcome.texture != null)
            StorySessionCache.SetAnchorPageTexture(outcome.texture);

        StorySessionCache.RecordCompletedPage(new StorySessionCache.PageRecord
        {
            pageId = page?.pageId ?? "",
            pageTitle = page?.pageTitle ?? "",
            sceneGuideText = page?.sceneGuideText ?? "",
            voiceGuideText = page?.voiceGuideText ?? "",
            userVoiceAnswer = voiceSupplement,
            generatedStoryText = string.IsNullOrWhiteSpace(voiceSupplement)
                ? $"（占位）{page?.pageTitle} 的故事段落待接入大模型。"
                : voiceSupplement.Trim(),
            generationPrompt = prompt,
            generatedImageNote = $"img2img，参考角色 {references.characterCount} 个" +
                                 (references.hasAnchor ? " + P1 锚图" : "") +
                                 (string.IsNullOrWhiteSpace(voiceSupplement) ? "" : " + 语音补充"),
            generatedImageUrl = outcome.imageUrl ?? "",
        }, outcome.texture, _pageIndex);

        SetPhase(CreationPhase.PageDone);
        SetStatus("本页创作完成，可进入下一页。");
        Debug.Log(
            $"[StoryCreation] 页完成 page={page?.pageId}，历史剧情摘要：\n{StorySessionCache.BuildPreviousPagesSummary()}");
    }

    IEnumerator RunVoiceInteractionCoroutine(
        StoryDefinition.StoryPageDefinition page,
        List<StoryCreationGapAnalyzer.Gap> gaps,
        System.Action<string> onComplete)
    {
        if (_voiceGateway == null)
        {
            onComplete?.Invoke("");
            yield break;
        }

        var questions = new List<string>();
        yield return FetchQuestionsCoroutine(page, gaps, questions);
        var answers = new List<string>();

        for (int i = 0; i < questions.Count; i++)
        {
            string question = questions[i];
            var gap = gaps[i];
            _currentVoiceQuestion = question;
            _currentGapKind = gap.kind.ToString();
            _currentGapRoleName = gap.roleName ?? "";
            if (answerTextInput != null)
                answerTextInput.text = "";
            BeginVoiceAnswerWindow();
            ShowVoiceQuestionText(question);
            SetStatus(BuildAnswerStatusHint(i, questions.Count));

            bool spoke = false;
            string ttsError = "";
            yield return _voiceGateway.SpeakText(question, (success, err) =>
            {
                spoke = success;
                ttsError = err;
            });
            if (!spoke)
                SetStatus($"（语音播放失败：{ttsError}，请看屏幕文字）{i + 1}/{questions.Count}");

            yield return WaitForChildVoiceAnswer();
            if (!string.IsNullOrWhiteSpace(_pendingVoiceError))
            {
                SetStatus(_pendingVoiceError);
                yield return new WaitForSeconds(0.5f);
                i--;
                continue;
            }

            if (string.IsNullOrWhiteSpace(_pendingVoiceTranscript))
            {
                SetStatus("未收到回答，请再试一次");
                yield return new WaitForSeconds(0.5f);
                i--;
                continue;
            }

            answers.Add(_pendingVoiceTranscript.Trim());
        }

        if (_guideText != null && page != null)
            _guideText.text = page.sceneGuideText ?? "";
        ClearVoiceQuestionText();
        if (answerTextInput != null)
            answerTextInput.text = "";
        if (_answerUiRoot != null)
            _answerUiRoot.SetActive(false);

        string supplement = BuildVoiceSupplement(gaps, answers);
        onComplete?.Invoke(supplement);
    }

    IEnumerator FetchQuestionsCoroutine(
        StoryDefinition.StoryPageDefinition page,
        List<StoryCreationGapAnalyzer.Gap> gaps,
        List<string> outQuestions)
    {
        outQuestions.Clear();
        if (gaps == null || gaps.Count == 0)
            yield break;

        var questions = new string[gaps.Count];
        var aiGaps = new List<StoryCreationGapAnalyzer.Gap>();
        var aiGapIndices = new List<int>();

        for (int i = 0; i < gaps.Count; i++)
        {
            var gap = gaps[i];
            if (gap.kind == StoryCreationGapAnalyzer.GapKind.OptionalStoryElement)
            {
                questions[i] = gap.fallbackQuestion;
                continue;
            }

            aiGapIndices.Add(i);
            aiGaps.Add(gap);
        }

        if (aiGaps.Count > 0 && useAiGeneratedQuestions)
        {
            var req = new StoryCreationVoiceGateway.StoryCreationQuestionsRequest
            {
                storyTitle = StorySessionCache.StoryTitle,
                pageTitle = page?.pageTitle ?? "",
                sceneGuideText = page?.sceneGuideText ?? "",
                previousSummary = StorySessionCache.BuildPreviousPagesSummary(),
                gaps = BuildGapDtos(aiGaps),
            };

            List<StoryCreationVoiceGateway.StoryCreationQuestion> remote = null;
            string err = "";
            yield return _voiceGateway.FetchQuestions(req, (qs, e) =>
            {
                remote = qs;
                err = e;
            });

            if (remote != null && remote.Count >= aiGaps.Count)
            {
                for (int j = 0; j < aiGaps.Count; j++)
                    questions[aiGapIndices[j]] = remote[j].text.Trim();
            }
            else
            {
                if (useAiGeneratedQuestions && !string.IsNullOrEmpty(err))
                    Debug.LogWarning($"[StoryCreation] AI 提问生成失败，使用本地话术：{err}");
                for (int j = 0; j < aiGaps.Count; j++)
                    questions[aiGapIndices[j]] = aiGaps[j].fallbackQuestion;
            }
        }
        else if (aiGaps.Count > 0)
        {
            for (int j = 0; j < aiGaps.Count; j++)
                questions[aiGapIndices[j]] = aiGaps[j].fallbackQuestion;
        }

        for (int i = 0; i < questions.Length; i++)
        {
            string q = questions[i];
            if (string.IsNullOrWhiteSpace(q))
                q = gaps[i].fallbackQuestion;
            outQuestions.Add(q.Trim());
        }
    }

    void ShowVoiceQuestionText(string question)
    {
        var target = voiceQuestionText != null ? voiceQuestionText : _guideText;
        if (target != null)
            target.text = question ?? "";
    }

    void ClearVoiceQuestionText()
    {
        if (voiceQuestionText != null)
            voiceQuestionText.text = "";
    }

    static StoryCreationVoiceGateway.StoryCreationGapDto[] BuildGapDtos(
        List<StoryCreationGapAnalyzer.Gap> gaps)
    {
        var arr = new StoryCreationVoiceGateway.StoryCreationGapDto[gaps.Count];
        for (int i = 0; i < gaps.Count; i++)
        {
            var g = gaps[i];
            arr[i] = new StoryCreationVoiceGateway.StoryCreationGapDto
            {
                kind = g.kind.ToString(),
                roleName = g.roleName ?? "",
                fallbackQuestion = g.fallbackQuestion ?? "",
            };
        }
        return arr;
    }

    static StoryCreationVoiceGateway.StoryCreationPromptRefineRequest ToPromptRefineRequest(
        StoryPageGenerationPipeline.PromptInputBundle bundle)
    {
        return new StoryCreationVoiceGateway.StoryCreationPromptRefineRequest
        {
            storyTitle = bundle.storyTitle ?? "",
            pageTitle = bundle.pageTitle ?? "",
            stylePromptPrefix = bundle.stylePromptPrefix ?? "",
            sceneGuideText = bundle.sceneGuideText ?? "",
            previousSummary = bundle.previousSummary ?? "",
            voiceSupplement = bundle.voiceSupplement ?? "",
            detectedRolesDescription = bundle.detectedRolesDescription ?? "",
            referenceImageClause = bundle.referenceImageClause ?? "",
            isContinuationPage = bundle.isContinuationPage,
        };
    }

    static string BuildVoiceSupplement(
        List<StoryCreationGapAnalyzer.Gap> gaps,
        List<string> answers)
    {
        if (answers == null || answers.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        int count = Mathf.Min(gaps.Count, answers.Count);
        for (int i = 0; i < count; i++)
        {
            if (sb.Length > 0)
                sb.Append(' ');
            var gap = gaps[i];
            if (gap.kind == StoryCreationGapAnalyzer.GapKind.CharacterBehavior &&
                !string.IsNullOrWhiteSpace(gap.roleName))
                sb.Append($"{gap.roleName}：{answers[i]}。");
            else if (gap.kind == StoryCreationGapAnalyzer.GapKind.OptionalStoryElement)
                sb.Append($"本页补充：{answers[i]}。");
            else
                sb.Append(answers[i]).Append('。');
        }
        return sb.ToString().Trim();
    }

    void BeginVoiceAnswerWindow()
    {
        _waitingForVoiceAnswer = true;
        _pendingVoiceTranscript = "";
        _pendingVoiceError = "";
    }

    IEnumerator WaitForChildVoiceAnswer()
    {
        while (_waitingForVoiceAnswer)
            yield return null;
    }

    void OnAnswerVoiceDown(Button button)
    {
        if (_phase != CreationPhase.VoiceInteracting || _voiceGateway == null)
            return;
        if (allowTextAnswerInput && _answerInputMode == AnswerInputMode.Text)
            return;
        if (!_voiceGateway.BeginRecording())
        {
            SetStatus("无法开始录音，请检查麦克风权限。");
            return;
        }

        SetAnswerVoiceLabel(button, "松开上传");
        SetStatus("正在听…");
    }

    void OnAnswerVoiceUp(Button button)
    {
        if (_phase != CreationPhase.VoiceInteracting || _voiceGateway == null)
            return;
        if (allowTextAnswerInput && _answerInputMode == AnswerInputMode.Text)
            return;

        SetAnswerVoiceLabel(button, "按住说话");
        if (!_voiceGateway.EndRecordingAndEncode(out var wav, out var encodeError))
        {
            if (_waitingForVoiceAnswer)
            {
                _pendingVoiceError = encodeError;
                _waitingForVoiceAnswer = false;
            }
            else
            {
                SetStatus(encodeError);
            }
            return;
        }

        StartCoroutine(TranscribeVoiceAnswerCoroutine(wav));
    }

    IEnumerator TranscribeVoiceAnswerCoroutine(byte[] wav)
    {
        SetStatus("正在识别你的回答…");
        var asrContext = new StoryCreationVoiceGateway.AsrContext
        {
            gapKind = _currentGapKind ?? "",
            roleName = _currentGapRoleName ?? "",
        };
        string transcript = "";
        string error = "";
        yield return _voiceGateway.TranscribeWav(wav, asrContext, (t, e) =>
        {
            transcript = t;
            error = e;
        });

        if (_waitingForVoiceAnswer)
        {
            _voiceGateway?.StopPlayback();
            _pendingVoiceTranscript = transcript;
            _pendingVoiceError = string.IsNullOrWhiteSpace(transcript) ? (error ?? "没听清，请再试一次") : "";
            _waitingForVoiceAnswer = false;
            SetStatus(string.IsNullOrWhiteSpace(_pendingVoiceError) ? "收到！准备下一问…" : _pendingVoiceError);
            yield break;
        }

        SetStatus("回答来得太早，请等提问出现后再说一次");
    }

    static void SetAnswerVoiceLabel(Button button, string text)
    {
        if (button == null)
            return;
        var label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.text = text;
    }

    void OnRebuildClicked()
    {
        StopAllCoroutines();
        _voiceGateway?.CancelRequest();
        _voiceGateway?.StopMicIfAny();
        _waitingForVoiceAnswer = false;
        SetCameraPreviewExpanded(false);
        ClearGeneratedPageOverlay();
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
            var saveId = CompletedStoryStore.SaveFromSession();
            if (!string.IsNullOrWhiteSpace(saveId))
                Debug.Log($"[StoryCreation] 绘本已保存 saveId={saveId}");
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
