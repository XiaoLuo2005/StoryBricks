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
    [Tooltip("成图后右下角固定展示的绘本故事文案")]
    public GameObject pageCaptionPanel;
    public TextMeshProUGUI pageCaptionText;
    [Tooltip("留空则尝试 Resources/UI/word SDF；推荐拖入 Assets/Art/word SDF")]
    public TMP_FontAsset pageCaptionFont;

    public bool IsComplete =>
        canvas != null &&
        backgroundImage != null &&
        generatedPageImage != null &&
        guideText != null &&
        confirmButton != null &&
        cameraPreviewMini != null;
}
