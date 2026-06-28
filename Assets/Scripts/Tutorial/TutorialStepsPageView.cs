using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 积木搭建教程页 UI 引用集合。Prefab 模式可拖拽 StepViewer、BottomControls、
/// LelePanelRoot、MascotRoot 等节点微调位置。
/// </summary>
[DisallowMultipleComponent]
public class TutorialStepsPageView : MonoBehaviour
{
    [Header("Canvas")]
    public Canvas canvas;
    public CanvasScaler canvasScaler;

    [Header("Top")]
    public TextMeshProUGUI titleText;
    public Button backButton;
    public Button preview3DButton;

    [Header("Step Viewer")]
    public Image stepImage;
    public CanvasGroup stepFadeGroup;
    public RectTransform stepSwipeZone;

    [Header("Bottom")]
    public TextMeshProUGUI stepLabelText;
    public Slider progressSlider;
    public Button prevButton;
    public Button nextButton;

    [Header("乐乐")]
    [Tooltip("拖 LelePanelRoot 可移动整块助手区域；展开 LelePanel 可微调对话框、监听条")]
    public RectTransform lelePanelRoot;
    public TutorialLelePanelView lelePanel;
    [Tooltip("Lottie 吉祥物会生成在此节点下；留空则使用左下角默认位置")]
    public RectTransform mascotRoot;

    [Header("Logic")]
    public StepViewerUI stepViewer;

    public bool IsComplete =>
        canvas != null &&
        stepImage != null &&
        stepLabelText != null &&
        prevButton != null &&
        nextButton != null &&
        progressSlider != null &&
        stepViewer != null;
}
