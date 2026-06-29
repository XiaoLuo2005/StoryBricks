using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 故事创作页 UI 引用集合。Prefab / 场景模式可在 Hierarchy 直接调整布局。
/// </summary>
[DisallowMultipleComponent]
public class StoryCreationPageView : MonoBehaviour
{
    [Header("Canvas")]
    public Canvas canvas;
    public CanvasScaler canvasScaler;

    [Header("Page")]
    public Image backgroundImage;
    public RawImage generatedPageImage;
    public Text pageIndicatorText;
    public Text guideText;
    [Tooltip("可选：单独显示语音提问；留空则用 GuideText")]
    public Text voiceQuestionText;

    [Header("Navigation")]
    public Button backButton;

    [Header("Actions")]
    public Button voiceGuideButton;
    public Button confirmButton;
    public Button rebuildButton;
    public Button regenerateButton;
    public Button nextPageButton;

    [Header("Status")]
    public GameObject statusPanel;
    public Text statusText;

    [Header("Camera Preview")]
    public RawImage cameraPreviewMini;
    public Button cameraPreviewMiniButton;
    public GameObject cameraPreviewOverlay;
    public RawImage cameraPreviewExpanded;
    public Button cameraPreviewOverlayBackdropButton;
    public Button cameraPreviewExpandedPanelButton;

    [Header("Voice Answer")]
    public GameObject answerUiRoot;
    public GameObject answerVoicePanel;
    public GameObject answerTextPanel;
    public Button answerVoiceButton;
    public Button answerModeVoiceButton;
    public Button answerModeTextButton;
    public InputField answerTextInput;
    public Button answerTextSubmitButton;

    [Header("Page Story Caption")]
    [Tooltip("点击「故事阅读」后弹出的故事叙述面板（默认隐藏）")]
    public GameObject pageCaptionPanel;
    public TextMeshProUGUI pageCaptionText;
    [Tooltip("留空则尝试 Resources/UI/word SDF；推荐拖入 Assets/Art/word SDF")]
    public TMP_FontAsset pageCaptionFont;
    [Tooltip("按下后显示/隐藏故事叙述面板")]
    public Button storyToggleButton;
    [Tooltip("叙述面板内的「收起」按钮，可选")]
    public Button storyCloseButton;

    public bool IsComplete =>
        canvas != null &&
        backgroundImage != null &&
        generatedPageImage != null &&
        guideText != null &&
        confirmButton != null &&
        cameraPreviewMini != null;

    public bool WireFromSceneHierarchy()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        if (canvasScaler == null)
            canvasScaler = GetComponent<CanvasScaler>();

        if (canvas == null)
            return false;

        var root = canvas.transform;

        if (backgroundImage == null)
        {
            var bg = root.Find("Background");
            if (bg != null)
                backgroundImage = bg.GetComponent<Image>();
        }

        if (generatedPageImage == null)
        {
            var gen = root.Find("GeneratedPage");
            if (gen != null)
                generatedPageImage = gen.GetComponent<RawImage>();
        }

        if (backButton == null)
        {
            var back = root.Find("BackButton");
            if (back != null)
                backButton = back.GetComponent<Button>();
        }

        if (pageIndicatorText == null)
        {
            var indicator = root.Find("PageIndicator");
            if (indicator != null)
                pageIndicatorText = indicator.GetComponent<Text>();
        }

        if (guideText == null)
        {
            var guide = root.Find("GuideText");
            if (guide != null)
                guideText = guide.GetComponent<Text>();
        }

        if (voiceQuestionText == null)
        {
            var voiceQ = root.Find("VoiceQuestionText");
            if (voiceQ != null)
                voiceQuestionText = voiceQ.GetComponent<Text>();
        }

        WireButton(root, "VoiceGuideButton", ref voiceGuideButton);
        WireButton(root, "ConfirmButton", ref confirmButton);
        WireButton(root, "RebuildButton", ref rebuildButton);
        WireButton(root, "RegenerateButton", ref regenerateButton);
        WireButton(root, "NextPageButton", ref nextPageButton);
        WireButton(root, "StoryToggleButton", ref storyToggleButton);

        if (statusPanel == null)
        {
            var status = root.Find("StatusPanel");
            if (status != null)
                statusPanel = status.gameObject;
        }

        if (statusText == null && statusPanel != null)
            statusText = statusPanel.GetComponentInChildren<Text>(true);

        WireCameraPreview(root);
        WireAnswerInput(root);

        if (pageCaptionPanel == null)
        {
            var panel = root.Find("PageCaptionPanel");
            if (panel != null)
                pageCaptionPanel = panel.gameObject;
        }

        if (pageCaptionText == null && pageCaptionPanel != null)
        {
            pageCaptionText = CompletedStoryRuntimeUi.ResolveScrollableStoryText(
                pageCaptionPanel.transform,
                "CaptionScroll",
                "CaptionText");
        }

        if (storyCloseButton == null && pageCaptionPanel != null)
        {
            var close = pageCaptionPanel.transform.Find("StoryCloseButton");
            if (close != null)
                storyCloseButton = close.GetComponent<Button>();
        }

        return IsComplete;
    }

    void WireCameraPreview(Transform root)
    {
        if (cameraPreviewMini == null)
        {
            var mini = root.Find("CameraPreviewMini");
            if (mini != null)
                cameraPreviewMini = mini.GetComponent<RawImage>();
        }

        if (cameraPreviewMiniButton == null && cameraPreviewMini != null)
            cameraPreviewMiniButton = cameraPreviewMini.GetComponent<Button>();

        if (cameraPreviewOverlay == null)
        {
            var overlay = root.Find("CameraPreviewOverlay");
            if (overlay != null)
                cameraPreviewOverlay = overlay.gameObject;
        }

        if (cameraPreviewOverlay != null)
        {
            if (cameraPreviewExpanded == null)
            {
                var expandedPreview = cameraPreviewOverlay.transform.Find("ExpandedPanel/ExpandedPreview");
                if (expandedPreview != null)
                    cameraPreviewExpanded = expandedPreview.GetComponent<RawImage>();
            }

            WireButton(cameraPreviewOverlay.transform, "Backdrop", ref cameraPreviewOverlayBackdropButton);
            WireButton(cameraPreviewOverlay.transform, "ExpandedPanel", ref cameraPreviewExpandedPanelButton);
        }
    }

    void WireAnswerInput(Transform root)
    {
        if (answerUiRoot == null)
        {
            var answerRoot = root.Find("AnswerInputRoot");
            if (answerRoot != null)
                answerUiRoot = answerRoot.gameObject;
        }

        if (answerUiRoot == null)
            return;

        if (answerVoicePanel == null)
        {
            var voicePanel = answerUiRoot.transform.Find("AnswerVoicePanel");
            if (voicePanel != null)
                answerVoicePanel = voicePanel.gameObject;
        }

        if (answerTextPanel == null)
        {
            var textPanel = answerUiRoot.transform.Find("AnswerTextPanel");
            if (textPanel != null)
                answerTextPanel = textPanel.gameObject;
        }

        if (answerVoicePanel != null)
            WireButton(answerVoicePanel.transform, "AnswerVoiceButton", ref answerVoiceButton);

        if (answerTextPanel != null)
        {
            WireButton(answerTextPanel.transform, "AnswerModeVoice", ref answerModeVoiceButton);
            WireButton(answerTextPanel.transform, "AnswerModeText", ref answerModeTextButton);
            WireButton(answerTextPanel.transform, "AnswerTextSubmit", ref answerTextSubmitButton);

            if (answerTextInput == null)
            {
                var input = answerTextPanel.transform.Find("AnswerTextInput");
                if (input != null)
                    answerTextInput = input.GetComponent<InputField>();
            }
        }
    }

    static void WireButton(Transform parent, string name, ref Button target)
    {
        if (target != null || parent == null)
            return;

        var t = parent.Find(name);
        if (t != null)
            target = t.GetComponent<Button>();
    }

    static void WireButton(Canvas canvas, string name, ref Button target)
    {
        if (canvas != null)
            WireButton(canvas.transform, name, ref target);
    }
}
