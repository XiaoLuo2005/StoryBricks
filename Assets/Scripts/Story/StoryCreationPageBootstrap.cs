using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    const float CaptureCountdownSeconds = 3f;

    public string fallbackLibrarySceneName = StoryFlowScenes.StoryLibrary;
    public string backSceneName = StoryFlowScenes.StoryWorks;
    public string finishSceneName = StoryFlowScenes.StoryWorks;

    [Header("生图")]
    [Tooltip("本机调试填 http://127.0.0.1:8800/generate；云服务器需 nginx 放宽 body 限制")]
    public string imageGenServerUrl = "http://127.0.0.1:8800/generate";

    [Header("语音交互")]
    [Tooltip("storybricks-tutor-gateway 地址，用于 TTS 提问与 ASR 收录")]
    public string tutorGatewayUrl = "http://127.0.0.1:8787";

    [Tooltip("可选：在场景中自行摆放后拖入；留空则运行时自动创建语音状态指示")]
    public Button answerVoiceButton;

    [Tooltip("可选：专门显示当前语音提问全文；留空则用左下角 GuideText")]
    public Text voiceQuestionText;

    [Tooltip("生图前是否用大模型整理各来源文本为连贯场景描述；失败则回退本地拼接")]
    public bool useAiPromptRefinement = true;

    [Tooltip("允许在语音问答时切换为文字输入（宿舍测试用）")]
    public bool allowTextAnswerInput = false;

    [Tooltip("精简 UI：隐藏重复文案/状态条，按阶段只显示必要按钮")]
    public bool simplifiedUi = true;

    [Tooltip("进入语音问答时的默认回答方式")]
    public AnswerInputMode defaultAnswerInputMode = AnswerInputMode.Voice;

    [Tooltip("可选：文字输入框；留空则运行时自动创建")]
    public InputField answerTextInput;

    [Tooltip("可选：文字提交按钮")]
    public Button answerTextSubmitButton;

    [Tooltip("可选：切换到语音回答")]
    public Button answerModeVoiceButton;

    [Tooltip("可选：切换到文字回答")]
    public Button answerModeTextButton;

    [Tooltip("进入页面时自动播放 voiceGuideText")]
    public bool autoPlayGuideOnPage = true;

    [Header("绘本页故事文案")]
    [Tooltip("固定区域展示字体，推荐拖入 Assets/Art/word SDF")]
    public TMP_FontAsset pageCaptionFont;
    [Tooltip("AI 生成旁白最大字数（含标点）")]
    public int pageCaptionMaxChars = StoryPageCaptionArt.DefaultMaxChars;

    [Header("UI")]
    [Tooltip("拖入场景里的 StoryCreationCanvas，或留空并在下方指定 Prefab")]
    public StoryCreationPageView pageView;
    [Tooltip("留空且 pageView 为空时，从 Resources 加载默认 Prefab")]
    public StoryCreationPageView pageViewPrefab;
    [Tooltip("仅在 pageView 与 pageViewPrefab 都为空时，运行时临时搭建 UI（不可视化编辑）")]
    public bool allowRuntimeFallbackUi = true;

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
    Button _regenerateButton;
    Button _nextPageButton;
    GameObject _answerUiRoot;
    GameObject _answerVoicePanel;
    GameObject _answerTextPanel;
    GameObject _pageCaptionPanel;
    TextMeshProUGUI _pageCaptionText;
    AnswerInputMode _answerInputMode = AnswerInputMode.Voice;
    ArUcoDetector _arUcoDetector;
    LocalImageGenClient _imageGenClient;
    StoryCreationVoiceGateway _voiceGateway;
    StoryCreationArDirector _arDirector;
    StoryCreationLeleHost _leleHost;
    RectTransform _canvasRoot;

    bool _waitingForVoiceAnswer;
    string _pendingVoiceTranscript;
    string _pendingVoiceError;
    string _currentVoiceQuestion;
    string _currentGapKind;
    string _currentGapRoleName;
    string _lastPageSummary = "";
    string _lastPageCaption = "";
    string _lastConversationLog = "";
    string _lastVoiceSupplement = "";
    Coroutine _autoGuideCoroutine;

    /// <summary>创作页摄像头与 ArUco 检测器，供后续识别/生图流程读取。</summary>
    public ArUcoDetector CameraDetector => _arUcoDetector;

    static Font BuiltinUIFont => StoryCreationPageUiBuilder.BuiltinUIFont;

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
        EnsurePageView();
        if (pageView == null || !pageView.IsComplete)
        {
            Debug.LogError(
                "StoryCreation: 未找到可用的 StoryCreationPageView。" +
                "请运行 StoryBricks/创作页/当前场景挂载可视化 UI。");
            return;
        }

        BindPageView();
        SetupCameraDetector();
        SetupArDirector();
        SetupImageGeneration();
        SetupVoiceGateway();
        SetupLeleHost();
        ShowCurrentPage();
        SetPhase(CreationPhase.Building);
        ApplySimplifiedChrome();
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void EnsurePageView()
    {
        if (pageView != null && pageView.IsComplete)
            return;

        if (pageViewPrefab == null)
            pageViewPrefab = Resources.Load<StoryCreationPageView>("UI/StoryCreationPage");

        if (pageViewPrefab != null)
        {
            pageView = Instantiate(pageViewPrefab);
            pageView.name = pageViewPrefab.name;
            return;
        }

        if (!allowRuntimeFallbackUi)
            return;

        Debug.LogWarning(
            "[StoryCreationPageBootstrap] 未配置 pageView / Prefab，正在运行时临时搭建 UI。" +
            "请运行 StoryBricks/创作页/创建 StoryCreation UI Prefab。");
        pageView = StoryCreationPageUiBuilder.BuildPageView(null);
    }

    void BindPageView()
    {
        _canvasRoot = pageView.canvas != null
            ? pageView.canvas.GetComponent<RectTransform>()
            : null;
        _backgroundImage = pageView.backgroundImage;
        _generatedPageImage = pageView.generatedPageImage;
        _pageIndicatorText = pageView.pageIndicatorText;
        _guideText = pageView.guideText;
        if (voiceQuestionText == null)
            voiceQuestionText = pageView.voiceQuestionText;
        _voiceGuideButton = pageView.voiceGuideButton;
        _confirmButton = pageView.confirmButton;
        _rebuildButton = pageView.rebuildButton;
        _regenerateButton = pageView.regenerateButton;
        _nextPageButton = pageView.nextPageButton;
        _statusPanel = pageView.statusPanel;
        _statusText = pageView.statusText;
        _cameraPreviewMini = pageView.cameraPreviewMini;
        _cameraPreviewExpanded = pageView.cameraPreviewExpanded;
        _cameraPreviewOverlay = pageView.cameraPreviewOverlay;
        _answerUiRoot = pageView.answerUiRoot;
        _answerVoicePanel = pageView.answerVoicePanel;
        _answerTextPanel = pageView.answerTextPanel;

        if (answerVoiceButton == null)
            answerVoiceButton = pageView.answerVoiceButton;
        if (answerModeVoiceButton == null)
            answerModeVoiceButton = pageView.answerModeVoiceButton;
        if (answerModeTextButton == null)
            answerModeTextButton = pageView.answerModeTextButton;
        if (answerTextInput == null)
            answerTextInput = pageView.answerTextInput;
        if (answerTextSubmitButton == null)
            answerTextSubmitButton = pageView.answerTextSubmitButton;

        _pageCaptionPanel = pageView.pageCaptionPanel;
        _pageCaptionText = pageView.pageCaptionText;
        var captionFont = ResolveCaptionFont(pageView.pageCaptionFont ?? pageCaptionFont);
        if (_pageCaptionText != null)
            StoryPageCaptionArt.ApplyCaptionStyle(_pageCaptionText, captionFont, pageCaptionMaxChars);

        if (pageView.backButton != null)
            StoryFlowBackButtonUi.BindNavigation(pageView.backButton, "← 返回作品集", backSceneName);
        else if (pageView.canvas != null)
            StoryFlowBackButtonUi.EnsureTopLeft(pageView.canvas, "← 返回作品集", backSceneName);

        _voiceGuideButton.onClick.RemoveAllListeners();
        _confirmButton.onClick.RemoveAllListeners();
        _rebuildButton.onClick.RemoveAllListeners();
        if (_regenerateButton != null)
            _regenerateButton.onClick.RemoveAllListeners();
        _nextPageButton.onClick.RemoveAllListeners();
        _voiceGuideButton.onClick.AddListener(OnVoiceGuideClicked);
        _confirmButton.onClick.AddListener(OnConfirmClicked);
        _rebuildButton.onClick.AddListener(OnRebuildClicked);
        if (_regenerateButton != null)
            _regenerateButton.onClick.AddListener(OnRegenerateClicked);
        _nextPageButton.onClick.AddListener(OnNextPageClicked);

        if (answerVoiceButton != null)
            StoryCreationPageUiBuilder.ConfigureAnswerVoiceIndicator(answerVoiceButton);

        WireCameraPreviewUi();
        WireAnswerInputUi();
    }

    void WireCameraPreviewUi()
    {
        if (pageView.cameraPreviewMiniButton != null)
        {
            pageView.cameraPreviewMiniButton.onClick.RemoveAllListeners();
            pageView.cameraPreviewMiniButton.onClick.AddListener(() => SetCameraPreviewExpanded(true));
        }

        if (pageView.cameraPreviewOverlayBackdropButton != null)
        {
            pageView.cameraPreviewOverlayBackdropButton.onClick.RemoveAllListeners();
            pageView.cameraPreviewOverlayBackdropButton.onClick.AddListener(() => SetCameraPreviewExpanded(false));
        }

        if (pageView.cameraPreviewExpandedPanelButton != null)
        {
            pageView.cameraPreviewExpandedPanelButton.onClick.RemoveAllListeners();
            pageView.cameraPreviewExpandedPanelButton.onClick.AddListener(() => SetCameraPreviewExpanded(false));
        }
    }

    void WireAnswerInputUi()
    {
        if (_answerUiRoot != null)
            _answerUiRoot.SetActive(false);

        if (!allowTextAnswerInput)
            return;

        if (answerModeVoiceButton != null)
        {
            answerModeVoiceButton.onClick.RemoveAllListeners();
            answerModeVoiceButton.onClick.AddListener(() => SetAnswerInputMode(AnswerInputMode.Voice));
        }

        if (answerModeTextButton != null)
        {
            answerModeTextButton.onClick.RemoveAllListeners();
            answerModeTextButton.onClick.AddListener(() => SetAnswerInputMode(AnswerInputMode.Text));
        }

        if (answerTextInput != null)
        {
            answerTextInput.onEndEdit.RemoveAllListeners();
            answerTextInput.onEndEdit.AddListener(OnAnswerTextEndEdit);
        }

        if (answerTextSubmitButton != null)
        {
            answerTextSubmitButton.onClick.RemoveAllListeners();
            answerTextSubmitButton.onClick.AddListener(OnAnswerTextSubmit);
        }

        SetAnswerInputMode(defaultAnswerInputMode);
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

    void SetupArDirector()
    {
        if (_cameraPreviewMini == null || _canvasRoot == null)
            return;

        _arDirector = GetComponent<StoryCreationArDirector>();
        if (_arDirector == null)
            _arDirector = gameObject.AddComponent<StoryCreationArDirector>();

        _arDirector.Initialize(_canvasRoot, _cameraPreviewMini, _cameraPreviewExpanded, _arUcoDetector, BuiltinUIFont);
        _arDirector.SetPageContext(GetCurrentPage(), _characterReferences, _markerTaxonomy);
        _arDirector.SetActive(_phase == CreationPhase.Building || _phase == CreationPhase.Capturing);
        WireArDirectorEvents();
    }

    void WireArDirectorEvents()
    {
        if (_arDirector == null)
            return;

        _arDirector.CharacterArrived -= OnCharacterArrived;
        _arDirector.CharacterMoved -= OnCharacterMoved;
        _arDirector.AllCharactersReady -= OnAllCharactersReady;
        _arDirector.RosterHintChanged -= OnRosterHintChanged;
        _arDirector.CharacterArrived += OnCharacterArrived;
        _arDirector.CharacterMoved += OnCharacterMoved;
        _arDirector.AllCharactersReady += OnAllCharactersReady;
        _arDirector.RosterHintChanged += OnRosterHintChanged;
    }

    void OnCharacterArrived(string roleName)
    {
        if (_phase != CreationPhase.Building || _leleHost == null)
            return;
        _leleHost.ReactPlacement($"{roleName}来啦！摆好位置，跟乐乐说说 ta 在干嘛。");
    }

    void OnCharacterMoved(string roleName)
    {
        if (_phase != CreationPhase.Building || _leleHost == null)
            return;
        if (string.IsNullOrWhiteSpace(roleName))
            return;
        _leleHost.ReactPlacement($"哇，{roleName}挪位置啦！现在想做什么呀？");
    }

    void OnAllCharactersReady()
    {
        if (_phase != CreationPhase.Building || _leleHost == null)
            return;
        _leleHost.ReactPlacement("伙伴都到齐啦！边玩边讲，好了就点「这页摆好了」。");
    }

    void OnRosterHintChanged(string hint)
    {
        _leleHost?.SetRosterHint(hint);
    }

    void SetupLeleHost()
    {
        if (_canvasRoot == null)
            return;

        _leleHost = GetComponent<StoryCreationLeleHost>();
        if (_leleHost == null)
            _leleHost = gameObject.AddComponent<StoryCreationLeleHost>();
        _leleHost.Initialize(_canvasRoot, _voiceGateway);
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

        if (voice && _phase == CreationPhase.VoiceInteracting && _waitingForVoiceAnswer)
            StartContinuousAnswerListeningIfVoiceMode();
        else if (!voice)
            StopContinuousAnswerListening();

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

        StopContinuousAnswerListening();
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

    Button ActiveAnswerVoiceButton => answerVoiceButton;

    void LateUpdate()
    {
        if (!_cameraPreviewExpandedOpen || _cameraPreviewExpanded == null || _cameraPreviewMini == null)
            return;
        if (_cameraPreviewExpanded.texture != _cameraPreviewMini.texture)
            _cameraPreviewExpanded.texture = _cameraPreviewMini.texture;
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
        _lastPageSummary = "";
        _lastPageCaption = "";
        _lastVoiceSupplement = "";
        _lastConversationLog = "";

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
                ? $"{_pageIndex + 1}/{_pages.Length}"
                : $"{_pageIndex + 1}/{_pages.Length} · {title}";
            _pageIndicatorText.alignment = simplifiedUi ? TextAnchor.UpperRight : TextAnchor.LowerRight;
        }

        string guide = page.sceneGuideText ?? "";
        if (!string.IsNullOrWhiteSpace(StorySessionCache.NextPageSceneHint))
        {
            guide = $"{StorySessionCache.NextPageSceneHint.Trim()}\n{guide}".Trim();
            StorySessionCache.ConsumeNextPageSceneHint();
        }

        if (_guideText != null)
        {
            _guideText.text = guide;
            _guideText.gameObject.SetActive(true);
        }

        ClearGeneratedPageOverlay();
        ClearPageCaption();
        UpdateActionButtons();
        SetStatus("");

        if (_arDirector != null)
        {
            _arDirector.SetPageContext(page, _characterReferences, _markerTaxonomy);
            _arDirector.ClearSpeechBubble();
        }

        if (_leleHost != null)
        {
            _leleHost.SetPageContext(StorySessionCache.StoryTitle, page.pageTitle, guide);
            string opening = string.IsNullOrWhiteSpace(page.voiceGuideText)
                ? $"第 {_pageIndex + 1} 页「{page.pageTitle}」。"
                : page.voiceGuideText.Trim();
            _leleHost.ResetDialog(opening);
        }

        if (_autoGuideCoroutine != null)
        {
            StopCoroutine(_autoGuideCoroutine);
            _autoGuideCoroutine = null;
        }

        if (autoPlayGuideOnPage && !string.IsNullOrWhiteSpace(page.voiceGuideText))
            _autoGuideCoroutine = StartCoroutine(AutoPlayGuideCoroutine(page.voiceGuideText));
    }

    IEnumerator AutoPlayGuideCoroutine(string text)
    {
        yield return new WaitForSeconds(0.35f);
        if (_phase != CreationPhase.Building)
            yield break;
        yield return PlayGuideVoiceCoroutine(text, appendToLele: true);
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

        ClearPageCaption();
    }

    void ClearPageCaption()
    {
        _lastPageCaption = "";
        if (_pageCaptionText != null)
            _pageCaptionText.text = "";
        if (_pageCaptionPanel != null)
            _pageCaptionPanel.SetActive(false);
    }

    void ShowPageCaption(string caption)
    {
        caption = StoryPageCaptionArt.Clamp(caption, pageCaptionMaxChars);
        _lastPageCaption = caption;
        if (_pageCaptionText == null)
            return;

        _pageCaptionText.text = caption;
        if (_pageCaptionPanel != null)
            _pageCaptionPanel.SetActive(!string.IsNullOrWhiteSpace(caption));
        if (_guideText != null && !string.IsNullOrWhiteSpace(caption))
            _guideText.gameObject.SetActive(false);
    }

    TMP_FontAsset ResolveCaptionFont(TMP_FontAsset assigned)
    {
        if (assigned != null)
            return assigned;
        if (pageCaptionFont != null)
            return pageCaptionFont;
#if UNITY_EDITOR
        var artFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/word SDF.asset");
        if (artFont != null)
            return artFont;
#endif
        return StoryPageCaptionArt.ResolveFont(null);
    }

    IEnumerator GeneratePageCaptionCoroutine(
        StoryDefinition.StoryPageDefinition page,
        string pageSummary,
        string conversationLog)
    {
        string caption = "";
        if (_voiceGateway != null)
        {
            yield return _voiceGateway.FetchPageCaption(
                new StoryCreationVoiceGateway.StoryCreationPageCaptionRequest
                {
                    storyTitle = StorySessionCache.StoryTitle,
                    pageTitle = page?.pageTitle ?? "",
                    sceneGuideText = page?.sceneGuideText ?? "",
                    previousSummary = StorySessionCache.BuildPreviousPagesSummary(),
                    pageSummary = pageSummary ?? "",
                    conversationLog = conversationLog ?? "",
                    maxChars = pageCaptionMaxChars,
                },
                (text, err) =>
                {
                    caption = text;
                    if (!string.IsNullOrEmpty(err))
                        Debug.LogWarning($"[StoryCreation] 绘本旁白生成失败：{err}");
                });
        }

        if (string.IsNullOrWhiteSpace(caption))
        {
            caption = StoryPageCaptionArt.FallbackFromScene(
                !string.IsNullOrWhiteSpace(pageSummary) ? pageSummary : page?.sceneGuideText,
                page?.pageTitle,
                pageCaptionMaxChars);
        }

        caption = StoryPageCaptionArt.Clamp(caption, pageCaptionMaxChars);
        ShowPageCaption(caption);
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
        if (_arDirector != null)
        {
            bool arActive = phase == CreationPhase.Building ||
                            phase == CreationPhase.Capturing ||
                            phase == CreationPhase.VoiceInteracting;
            _arDirector.SetActive(arActive);
        }

        if (phase != CreationPhase.VoiceInteracting)
            _voiceGateway?.StopAnswerListening();

        if (_leleHost != null)
        {
            bool freeChat = phase == CreationPhase.Building;
            _leleHost.SetFreeChatEnabled(freeChat);
        }
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
        if (_regenerateButton != null)
        {
            _regenerateButton.interactable = pageDone;
            _regenerateButton.gameObject.SetActive(pageDone);
        }
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
        ApplySimplifiedChrome();
    }

    void ApplySimplifiedChrome()
    {
        if (!simplifiedUi)
            return;

        bool busy = _phase == CreationPhase.Capturing || _phase == CreationPhase.Generating;
        bool building = _phase == CreationPhase.Building;
        bool voicePhase = _phase == CreationPhase.VoiceInteracting;
        bool pageDone = _phase == CreationPhase.PageDone;

        if (_guideText != null)
            _guideText.gameObject.SetActive(false);

        if (_voiceGuideButton != null)
            _voiceGuideButton.gameObject.SetActive(!autoPlayGuideOnPage && building && !busy);

        if (_pageIndicatorText != null)
            _pageIndicatorText.gameObject.SetActive(building || pageDone);

        if (_statusPanel != null)
            _statusPanel.SetActive(false);

        if (_answerUiRoot != null && !allowTextAnswerInput)
            _answerUiRoot.SetActive(false);

        if (_rebuildButton != null)
            _rebuildButton.gameObject.SetActive(building && !busy && !voicePhase);

        if (_confirmButton != null)
            _confirmButton.gameObject.SetActive(building && !busy && !voicePhase);

        if (_nextPageButton != null)
            _nextPageButton.gameObject.SetActive(pageDone);

        if (_regenerateButton != null)
            _regenerateButton.gameObject.SetActive(pageDone);

        if (_cameraPreviewMini != null)
        {
            bool showCam = building || voicePhase || _phase == CreationPhase.Capturing;
            _cameraPreviewMini.transform.parent.gameObject.SetActive(showCam);
        }

        if (_leleHost?.Panel != null)
            _leleHost.Panel.gameObject.SetActive(building || voicePhase || pageDone);
    }

    void SetStatus(string text)
    {
        if (simplifiedUi && _leleHost != null)
            _leleHost.SetStatus(text ?? "");

        if (_statusText == null)
            return;

        if (simplifiedUi)
        {
            if (_statusPanel != null)
                _statusPanel.SetActive(false);
            return;
        }

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

        StartCoroutine(PlayGuideVoiceCoroutine(page.voiceGuideText, appendToLele: true));
    }

    IEnumerator PlayGuideVoiceCoroutine(string text, bool appendToLele = false)
    {
        if (_voiceGateway == null)
        {
            SetStatus("语音服务未就绪。");
            yield break;
        }

        SetStatus("正在播放引导语音…");
        if (appendToLele && _leleHost != null)
            _leleHost.AppendLele(text);

        bool resumeFreeChat = _phase == CreationPhase.Building && _leleHost != null && _leleHost.IsFreeChatEnabled;
        if (resumeFreeChat)
            _leleHost.SetFreeChatEnabled(false);

        bool ok = false;
        string ttsError = "";
        yield return _voiceGateway.SpeakText(text, (success, err) =>
        {
            ok = success;
            ttsError = err;
        });

        if (resumeFreeChat && _phase == CreationPhase.Building)
            _leleHost.SetFreeChatEnabled(true);

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
        var page = GetCurrentPage();
        var detectedIds = StoryPageGenerationPipeline.CollectDetectedMarkerIds(_arUcoDetector);
        var validation = StoryPageGenerationPipeline.ValidateRequiredCharacters(page, detectedIds);
        if (!validation.ok)
        {
            SetStatus(StoryPageGenerationPipeline.FormatKidMissingMessage(
                validation.missingIds,
                _characterReferences));
            yield break;
        }

        if (_characterReferences == null || _characterReferences.Length == 0)
        {
            SetStatus("故事未配置 characterReferences 角色参考图。");
            yield break;
        }

        var markers = _arUcoDetector?.DetectedMarkers;
        var gaps = StoryCreationGapAnalyzer.Analyze(
            page,
            markers,
            _characterReferences,
            _markerTaxonomy);

        string voiceSupplement = "";
        if (gaps.Count > 0 || _leleHost != null)
        {
            SetPhase(CreationPhase.VoiceInteracting);
            yield return RunAmbientStoryCloseCoroutine(page, gaps, result => voiceSupplement = result ?? "");
            if (string.IsNullOrWhiteSpace(voiceSupplement))
            {
                SetPhase(CreationPhase.Building);
                SetStatus("还没定好故事，边玩边跟乐乐说说，再点「这页摆好了」。");
                yield break;
            }
        }

        if (string.IsNullOrWhiteSpace(_lastPageSummary))
            _lastPageSummary = voiceSupplement.Trim();

        SetPhase(CreationPhase.Capturing);
        for (int i = (int)CaptureCountdownSeconds; i > 0; i--)
        {
            SetStatus($"请移开手部，{i} 秒后抓拍…");
            yield return new WaitForSeconds(1f);
        }

        SetStatus("正在识别积木…");
        detectedIds = StoryPageGenerationPipeline.CollectDetectedMarkerIds(_arUcoDetector);
        validation = StoryPageGenerationPipeline.ValidateRequiredCharacters(page, detectedIds);
        if (!validation.ok)
        {
            SetPhase(CreationPhase.Building);
            SetStatus(StoryPageGenerationPipeline.FormatKidMissingMessage(
                validation.missingIds,
                _characterReferences));
            yield break;
        }

        yield return GeneratePageImageCoroutine(page, validation, voiceSupplement, isRegenerate: false);
    }

    IEnumerator RunAmbientStoryCloseCoroutine(
        StoryDefinition.StoryPageDefinition page,
        List<StoryCreationGapAnalyzer.Gap> gaps,
        System.Action<string> onComplete)
    {
        if (_voiceGateway == null)
        {
            onComplete?.Invoke(page?.sceneGuideText ?? "");
            yield break;
        }

        _leleHost?.SetFreeChatEnabled(false);
        SetStatus("乐乐在整理你刚才讲的故事…");

        string conversationLog = _leleHost?.BuildExtractConversationLog() ?? "";
        StoryCreationVoiceGateway.StoryCreationExtractResult extract = null;

        for (int pass = 0; pass < 3; pass++)
        {
            string extractErr = "";
            yield return _voiceGateway.FetchExtractPageStory(
                BuildExtractRequest(page, gaps, conversationLog),
                (r, e) =>
                {
                    extract = r;
                    extractErr = e;
                });

            if (extract == null)
            {
                if (!string.IsNullOrEmpty(extractErr))
                    Debug.LogWarning($"[StoryCreation] 故事整理失败：{extractErr}");
                extract = BuildLocalExtractFallback(page, gaps, conversationLog);
            }

            if (extract.conversationDone || string.IsNullOrWhiteSpace(extract.followUpQuestion))
                break;
            if (pass >= 2)
                break;

            yield return AskSingleOpenQuestionCoroutine(extract.followUpQuestion);
            conversationLog = _leleHost?.BuildExtractConversationLog() ?? conversationLog;
        }

        if (_guideText != null && page != null)
            _guideText.text = page.sceneGuideText ?? "";
        ClearVoiceQuestionText();
        if (answerTextInput != null)
            answerTextInput.text = "";
        if (_answerUiRoot != null)
            _answerUiRoot.SetActive(false);
        StopContinuousAnswerListening();

        string supplement = extract?.voiceSupplement?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(supplement))
            supplement = page?.sceneGuideText?.Trim() ?? "";

        string recap = extract?.recapLine?.Trim();
        if (string.IsNullOrWhiteSpace(recap))
            recap = $"我听说是：{supplement}";

        string confirmed = supplement;
        yield return RunSummaryConfirmationCoroutine(
            page,
            conversationLog,
            supplement,
            s => confirmed = s,
            recap);

        StoreConversationLog(conversationLog);
        onComplete?.Invoke(confirmed ?? supplement);
    }

    IEnumerator AskSingleOpenQuestionCoroutine(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || _voiceGateway == null)
            yield break;

        SetPhase(CreationPhase.VoiceInteracting);
        _leleHost?.AppendLele(question);
        ShowVoiceQuestionText(question);
        SetStatus("跟乐乐随便说说就行");

        yield return _voiceGateway.SpeakText(question);
        BeginVoiceAnswerWindow();
        StartContinuousAnswerListeningIfVoiceMode();
        yield return WaitForChildVoiceAnswer();
        StopContinuousAnswerListening();

        if (!string.IsNullOrWhiteSpace(_pendingVoiceError))
        {
            SetStatus(_pendingVoiceError);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_pendingVoiceTranscript))
        {
            SetStatus("没听清也没关系，我们先用刚才听到的");
            yield break;
        }

        string answer = _pendingVoiceTranscript.Trim();
        _leleHost?.AppendChild(answer);
        _leleHost?.AppendStoryDraft(answer);
    }

    StoryCreationVoiceGateway.StoryCreationExtractRequest BuildExtractRequest(
        StoryDefinition.StoryPageDefinition page,
        List<StoryCreationGapAnalyzer.Gap> gaps,
        string conversationLog)
    {
        return new StoryCreationVoiceGateway.StoryCreationExtractRequest
        {
            storyTitle = StorySessionCache.StoryTitle,
            pageTitle = page?.pageTitle ?? "",
            sceneGuideText = page?.sceneGuideText ?? "",
            previousSummary = StorySessionCache.BuildPreviousPagesSummary(),
            rosterHint = _leleHost?.RosterHint ?? "",
            conversationLog = conversationLog ?? "",
            detectedRoles = BuildDetectedRolesLabel(gaps),
            gaps = BuildGapDtos(gaps ?? new List<StoryCreationGapAnalyzer.Gap>()),
        };
    }

    static string BuildDetectedRolesLabel(List<StoryCreationGapAnalyzer.Gap> gaps)
    {
        if (gaps == null || gaps.Count == 0)
            return "";
        var names = new HashSet<string>();
        foreach (var gap in gaps)
        {
            if (!string.IsNullOrWhiteSpace(gap.roleName))
                names.Add(gap.roleName.Trim());
        }
        return string.Join("、", names);
    }

    static StoryCreationVoiceGateway.StoryCreationExtractResult BuildLocalExtractFallback(
        StoryDefinition.StoryPageDefinition page,
        List<StoryCreationGapAnalyzer.Gap> gaps,
        string conversationLog)
    {
        string log = conversationLog?.Trim() ?? "";
        string scene = page?.sceneGuideText?.Trim() ?? "";
        string supplement = log.Length > 0 ? log : scene;
        var behaviorGap = default(StoryCreationGapAnalyzer.Gap);
        var hasBehaviorGap = false;
        if (gaps != null)
        {
            foreach (var g in gaps)
            {
                if (g.kind != StoryCreationGapAnalyzer.GapKind.CharacterBehavior)
                    continue;
                behaviorGap = g;
                hasBehaviorGap = true;
                break;
            }
        }

        bool needMore = log.Length < 8 && hasBehaviorGap && !string.IsNullOrWhiteSpace(behaviorGap.roleName);
        return new StoryCreationVoiceGateway.StoryCreationExtractResult
        {
            voiceSupplement = supplement,
            recapLine = supplement.Length > 0 ? $"我听说是：{supplement}" : scene,
            missingField = needMore ? "behavior" : "none",
            followUpQuestion = needMore
                ? $"再跟乐乐说说，{behaviorGap.roleName}这一页在干什么呀？"
                : "",
            conversationDone = !needMore && !string.IsNullOrWhiteSpace(supplement),
        };
    }

    IEnumerator RunSummaryConfirmationCoroutine(
        StoryDefinition.StoryPageDefinition page,
        string conversationLog,
        string draftSupplement,
        System.Action<string> onConfirmed,
        string recapLineOverride = null)
    {
        string summary = draftSupplement;
        if (_voiceGateway != null && string.IsNullOrWhiteSpace(recapLineOverride))
        {
            yield return _voiceGateway.FetchPageSummary(
                new StoryCreationVoiceGateway.StoryCreationSummaryRequest
                {
                    storyTitle = StorySessionCache.StoryTitle,
                    pageTitle = page?.pageTitle ?? "",
                    sceneGuideText = page?.sceneGuideText ?? "",
                    previousSummary = StorySessionCache.BuildPreviousPagesSummary(),
                    conversationLog = conversationLog,
                },
                (text, err) =>
                {
                    if (!string.IsNullOrWhiteSpace(text))
                        summary = text.Trim();
                    else if (!string.IsNullOrEmpty(err))
                        Debug.LogWarning($"[StoryCreation] 摘要失败：{err}");
                });
        }

        _lastPageSummary = summary;
        string confirmPrompt = !string.IsNullOrWhiteSpace(recapLineOverride)
            ? $"{recapLineOverride.Trim()} 小朋友，这样对吗？说「对」我就去画，说「想改」我们再来一遍。"
            : $"好，{LeleVoiceAssistant.DisplayName}总结一下：{summary}。小朋友，这样对吗？说「对」我就去画，说「想改」我们再来一遍。";

        for (int confirmTurn = 0; confirmTurn < 3; confirmTurn++)
        {
            _leleHost?.AppendLele(confirmPrompt);
            ShowVoiceQuestionText(confirmPrompt);
            SetPhase(CreationPhase.VoiceInteracting);
            BeginVoiceAnswerWindow();
            yield return _voiceGateway.SpeakText(confirmPrompt);
            StartContinuousAnswerListeningIfVoiceMode();
            yield return WaitForChildVoiceAnswer();
            StopContinuousAnswerListening();
            ClearVoiceQuestionText();

            string response = _pendingVoiceTranscript?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(response))
            {
                SetStatus("没听清，再说「对」或「想改」");
                continue;
            }

            _leleHost?.AppendChild(response);
            var intent = ClassifyGapIntent(response);
            if (intent == "repeat_question" || intent == "clarify")
            {
                confirmPrompt =
                    $"好，{LeleVoiceAssistant.DisplayName}再说一遍：{summary}。觉得对了就说「对」，想改就说「想改」。";
                yield return _voiceGateway.SpeakText("好，乐乐再说一遍刚才的故事！");
                continue;
            }

            if (intent == "off_topic")
            {
                confirmPrompt = "我们先确认故事哦：觉得对了就说「对」，想改就说「想改」。";
                yield return _voiceGateway.SpeakText("哈哈，我们先把这个故事确认完好不好？");
                continue;
            }

            if (WantsEditAnswer(response))
            {
                _leleHost?.AppendLele("好的，我们再来编一遍！");
                onConfirmed?.Invoke("");
                yield break;
            }

            onConfirmed?.Invoke(summary);
            yield break;
        }

        onConfirmed?.Invoke(summary);
    }

    void StoreConversationLog(string conversationLog)
    {
        _lastConversationLog = conversationLog ?? "";
    }

    static bool WantsEditAnswer(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return text.Contains("改") ||
               text.Contains("不对") ||
               text.Contains("重来") ||
               text.Contains("不要") ||
               text.Contains("再讲") ||
               text.Contains("再说");
    }

    IEnumerator PlayWaitNarrationCoroutine(string pageSummary)
    {
        if (_voiceGateway == null)
            yield break;

        string narration = "";
        yield return _voiceGateway.FetchWaitNarration(
            new StoryCreationVoiceGateway.StoryCreationNarrationRequest
            {
                storyTitle = StorySessionCache.StoryTitle,
                pageTitle = GetCurrentPage()?.pageTitle ?? "",
                pageSummary = pageSummary ?? "",
            },
            (text, err) =>
            {
                narration = text;
                if (!string.IsNullOrEmpty(err))
                    Debug.LogWarning($"[StoryCreation] 等待旁白失败：{err}");
            });

        if (string.IsNullOrWhiteSpace(narration))
            narration = "我来把你的故事画成绘本，稍等一下哦。";

        _leleHost?.AppendLele(narration);
        yield return _voiceGateway.SpeakText(narration);
    }

    IEnumerator PlayPageRecapCoroutine(string pageSummary)
    {
        if (_voiceGateway == null)
            yield break;

        string recap = "";
        yield return _voiceGateway.FetchPageRecap(
            new StoryCreationVoiceGateway.StoryCreationRecapRequest
            {
                storyTitle = StorySessionCache.StoryTitle,
                pageTitle = GetCurrentPage()?.pageTitle ?? "",
                pageSummary = pageSummary ?? "",
                storySoFar = StorySessionCache.BuildStorySoFarNarrative(),
            },
            (text, err) =>
            {
                recap = text;
                if (!string.IsNullOrEmpty(err))
                    Debug.LogWarning($"[StoryCreation] 页末小结失败：{err}");
            });

        if (string.IsNullOrWhiteSpace(recap))
            recap = "这一页完成啦！你的故事越来越精彩了。";

        _leleHost?.AppendLele(recap);
        yield return _voiceGateway.SpeakText(recap);
    }

    IEnumerator FetchBranchHintCoroutine(StoryDefinition.StoryPageDefinition page, string pageSummary)
    {
        if (_voiceGateway == null || _pages == null || _pageIndex >= _pages.Length - 1)
            yield break;

        var nextPage = _pages[_pageIndex + 1];
        string hint = "";
        yield return _voiceGateway.FetchBranchHint(
            new StoryCreationVoiceGateway.StoryCreationBranchRequest
            {
                storyTitle = StorySessionCache.StoryTitle,
                nextPageTitle = nextPage?.pageTitle ?? "",
                pageSummary = pageSummary ?? "",
            },
            (text, err) =>
            {
                hint = text;
                if (!string.IsNullOrEmpty(err))
                    Debug.LogWarning($"[StoryCreation] 分支提示失败：{err}");
            });

        if (!string.IsNullOrWhiteSpace(hint))
            StorySessionCache.SetNextPageSceneHint(hint);
    }

    void ShowVoiceQuestionText(string question)
    {
        if (simplifiedUi)
            return;

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

    static string ClassifyGapIntent(string answer)
    {
        var t = LeleVoiceAssistant.Normalize(answer);
        if (string.IsNullOrEmpty(t))
            return "incomplete";

        string[] repeat =
        {
            "什么", "啥", "再说一遍", "再说一次", "没听清", "没听见", "听不清", "重复一遍", "再说一下",
        };
        foreach (var token in repeat)
        {
            if (t.Contains(token))
                return "repeat_question";
        }

        string[] clarify =
        {
            "什么意思", "听不懂", "不明白", "啥意思", "不懂", "听不明白",
        };
        foreach (var token in clarify)
        {
            if (t.Contains(token))
                return "clarify";
        }

        string[] offTopic =
        {
            "吃饭", "饿了", "回家", "妈妈", "爸爸", "玩游戏", "手机", "电视", "幼儿园", "睡觉",
        };
        foreach (var token in offTopic)
        {
            if (t.Contains(token) && t.Length <= 8)
                return "off_topic";
        }

        if (t.Length <= 1 || t == "不知道" || t == "随便" || t == "嗯" || t == "好")
            return "incomplete";

        return "answered";
    }

    void BeginVoiceAnswerWindow()
    {
        _waitingForVoiceAnswer = true;
        _pendingVoiceTranscript = "";
        _pendingVoiceError = "";
    }

    void StartContinuousAnswerListeningIfVoiceMode()
    {
        if (_voiceGateway == null || _phase != CreationPhase.VoiceInteracting)
            return;
        if (allowTextAnswerInput && _answerInputMode == AnswerInputMode.Text)
            return;

        _voiceGateway.StopAnswerListening();
        bool ok = _voiceGateway.StartAnswerListening(
            wav => StartCoroutine(TranscribeVoiceAnswerCoroutine(wav)),
            err =>
            {
                if (!_waitingForVoiceAnswer)
                {
                    SetStatus(err);
                    return;
                }

                _pendingVoiceError = err;
                _waitingForVoiceAnswer = false;
            },
            speaking => UpdateVoiceListeningLabel(speaking));

        if (!ok)
        {
            SetStatus("无法开始监听，请检查麦克风权限。");
            return;
        }

        StoryCreationPageUiBuilder.SetAnswerVoiceLabel(
            ActiveAnswerVoiceButton,
            LeleVoiceAssistant.ListeningHint);
        SetStatus($"{LeleVoiceAssistant.DisplayName}在听你说，说完停一下就可以");
    }

    void UpdateVoiceListeningLabel(bool speaking)
    {
        string label = speaking ? LeleVoiceAssistant.SpeakingHint : LeleVoiceAssistant.ListeningHint;
        if (!simplifiedUi || allowTextAnswerInput)
            StoryCreationPageUiBuilder.SetAnswerVoiceLabel(ActiveAnswerVoiceButton, label);
        if (simplifiedUi && _leleHost != null)
            _leleHost.SetListenLabel(label);
    }

    void StopContinuousAnswerListening()
    {
        _voiceGateway?.StopAnswerListening();
    }

    bool IsVoiceAnswerMode =>
        !allowTextAnswerInput || _answerInputMode == AnswerInputMode.Voice;

    IEnumerator WaitForChildVoiceAnswer()
    {
        while (_waitingForVoiceAnswer)
            yield return null;
    }

    IEnumerator TranscribeVoiceAnswerCoroutine(byte[] wav)
    {
        _voiceGateway?.PauseAnswerListening();
        SetStatus($"{LeleVoiceAssistant.DisplayName}在听…");
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

        if (!_waitingForVoiceAnswer)
        {
            SetStatus("回答来得太早，请等提问出现后再说");
            if (IsVoiceAnswerMode)
                _voiceGateway?.ResumeAnswerListening();
            yield break;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            _pendingVoiceError = error ?? "没听清，请再说一次";
            SetStatus(_pendingVoiceError);
            if (IsVoiceAnswerMode)
                StartContinuousAnswerListeningIfVoiceMode();
            yield break;
        }

        transcript = LeleVoiceAssistant.StripWakePrefix(transcript);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            SetStatus("请直接说你的回答");
            if (IsVoiceAnswerMode)
                StartContinuousAnswerListeningIfVoiceMode();
            yield break;
        }

        _voiceGateway?.StopPlayback();
        _pendingVoiceTranscript = transcript.Trim();
        _pendingVoiceError = "";
        _waitingForVoiceAnswer = false;
        SetStatus("收到！准备下一问…");
    }

    void OnRegenerateClicked()
    {
        if (_phase != CreationPhase.PageDone)
            return;
        StartCoroutine(RegeneratePageCoroutine());
    }

    IEnumerator RegeneratePageCoroutine()
    {
        var page = GetCurrentPage();
        if (page == null)
            yield break;

        SetPhase(CreationPhase.Building);
        _leleHost?.SetFreeChatEnabled(true);
        _leleHost?.AppendLele("想换个说法？继续边玩边告诉乐乐，好了再点「这页摆好了」。");
        SetStatus("");
        yield break;
    }

    IEnumerator GeneratePageImageCoroutine(
        StoryDefinition.StoryPageDefinition page,
        StoryPageGenerationPipeline.ValidationResult validation,
        string voiceSupplement,
        bool isRegenerate = false)
    {
        var references = StoryPageGenerationPipeline.CollectCharacterReferenceTextures(
            validation.detectedIds,
            _characterReferences,
            anchorTexture: null);

        if (references.characterCount == 0)
        {
            StoryPageGenerationPipeline.ReleaseTemporaryTextures(references);
            SetPhase(CreationPhase.Building);
            SetStatus("未找到角色参考图。");
            yield break;
        }

        SetPhase(CreationPhase.Generating);
        SetStatus("正在写这一页的绘本故事…");
        yield return GeneratePageCaptionCoroutine(page, _lastPageSummary, _lastConversationLog);
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
        StartCoroutine(PlayWaitNarrationCoroutine(_lastPageSummary));
        Debug.Log($"[StoryCreation] 生图 Prompt（最终）：\n{prompt}");

        var outcome = new LocalImageGenClient.GenerateOutcome();
        yield return _imageGenClient.GenerateImageAndWait(prompt, references.textures, outcome);
        StoryPageGenerationPipeline.ReleaseTemporaryTextures(references);

        if (!outcome.success)
        {
            SetPhase(isRegenerate ? CreationPhase.PageDone : CreationPhase.Building);
            SetStatus(isRegenerate ? "生图失败，仍保留上一版。" : "生图失败，请重试。");
            if (!isRegenerate)
                Debug.LogError($"[StoryCreation] 生图失败: {outcome.errorMessage}");
            yield break;
        }

        if (_generatedPageImage != null && outcome.texture != null)
        {
            if (_currentGeneratedTexture != null && isRegenerate)
                Destroy(_currentGeneratedTexture);
            _currentGeneratedTexture = outcome.texture;
            _generatedPageImage.texture = outcome.texture;
            _generatedPageImage.gameObject.SetActive(true);
        }

        if (!isRegenerate && _pageIndex == 0 && outcome.texture != null)
            StorySessionCache.SetAnchorPageTexture(outcome.texture);

        _lastVoiceSupplement = voiceSupplement ?? "";
        StorySessionCache.RecordCompletedPage(new StorySessionCache.PageRecord
        {
            pageId = page?.pageId ?? "",
            pageTitle = page?.pageTitle ?? "",
            sceneGuideText = page?.sceneGuideText ?? "",
            voiceGuideText = page?.voiceGuideText ?? "",
            userVoiceAnswer = voiceSupplement,
            generatedStoryText = !string.IsNullOrWhiteSpace(_lastPageCaption)
                ? _lastPageCaption
                : _lastPageSummary,
            generationPrompt = prompt,
            generatedImageNote = isRegenerate
                ? "重新生成"
                : $"img2img，参考角色 {references.characterCount} 个" +
                  (references.hasAnchor ? " + P1 锚图" : "") +
                  (string.IsNullOrWhiteSpace(voiceSupplement) ? "" : " + 语音补充"),
            generatedImageUrl = outcome.imageUrl ?? "",
        }, outcome.texture, _pageIndex);

        SetPhase(CreationPhase.PageDone);
        SetStatus(isRegenerate ? "新绘本好啦！" : "本页创作完成，可进入下一页。");
        yield return PlayPageRecapCoroutine(_lastPageSummary);
        if (!isRegenerate)
            yield return FetchBranchHintCoroutine(page, _lastPageSummary);
        if (!isRegenerate)
        {
            Debug.Log(
                $"[StoryCreation] 页完成 page={page?.pageId}，历史剧情摘要：\n{StorySessionCache.BuildPreviousPagesSummary()}");
        }
    }

    void OnRebuildClicked()
    {
        StopAllCoroutines();
        _voiceGateway?.CancelRequest();
        _voiceGateway?.StopMicIfAny();
        StopContinuousAnswerListening();
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
}
