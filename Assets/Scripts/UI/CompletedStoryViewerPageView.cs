using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>绘本阅读页 UI 引用。在 Prefab 或场景里可视化摆放后由 Root 绑定逻辑。</summary>
[DisallowMultipleComponent]
public class CompletedStoryViewerPageView : MonoBehaviour
{
    public Canvas canvas;
    public Image pageImage;
    public RectTransform storyReaderPanelRoot;
    public TextMeshProUGUI storyText;
    public Button recordButton;
    public Button playButton;
    public Button rerecordButton;
    public Text voiceStatusText;
    public Button prevPageButton;
    public Button nextPageButton;
    public Text pageIndicatorText;
    public Button exitButton;
    public Button storyToggleButton;
    [Tooltip("叙述面板内的「收起」按钮，可选")]
    public Button storyCloseButton;
    public Button vrToggleButton;
    public Button stereoToggleButton;
    public Text vrHintText;

    public bool IsComplete =>
        canvas != null &&
        pageImage != null &&
        storyText != null &&
        prevPageButton != null &&
        nextPageButton != null;

    public bool WireFromSceneHierarchy()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (canvas == null)
            return false;

        if (pageImage == null)
        {
            var pageGo = canvas.transform.Find("PageImage");
            if (pageGo != null)
                pageImage = pageGo.GetComponent<Image>();
        }

        if (storyReaderPanelRoot == null)
        {
            var panel = canvas.transform.Find("StoryReaderPanel");
            if (panel != null)
                storyReaderPanelRoot = panel.GetComponent<RectTransform>();
        }

        if (storyText == null && storyReaderPanelRoot != null)
        {
            storyText = CompletedStoryRuntimeUi.ResolveScrollableStoryText(
                storyReaderPanelRoot,
                "StoryTextScroll",
                "StoryText");
        }

        if (storyReaderPanelRoot != null)
        {
            var row = storyReaderPanelRoot.Find("VoiceRow");
            if (row != null)
            {
                if (recordButton == null)
                {
                    var record = row.Find("RecordButton");
                    if (record != null)
                        recordButton = record.GetComponent<Button>();
                }

                if (playButton == null)
                {
                    var play = row.Find("PlayButton");
                    if (play != null)
                        playButton = play.GetComponent<Button>();
                }

                if (rerecordButton == null)
                {
                    var rerecord = row.Find("RerecordButton");
                    if (rerecord != null)
                        rerecordButton = rerecord.GetComponent<Button>();
                }

                if (voiceStatusText == null)
                {
                    var status = row.Find("Status");
                    if (status != null)
                        voiceStatusText = status.GetComponent<Text>();
                }
            }
        }

        if (prevPageButton == null)
        {
            var prev = canvas.transform.Find("PrevPageButton");
            if (prev != null)
                prevPageButton = prev.GetComponent<Button>();
        }

        if (nextPageButton == null)
        {
            var next = canvas.transform.Find("NextPageButton");
            if (next != null)
                nextPageButton = next.GetComponent<Button>();
        }

        if (pageIndicatorText == null)
        {
            var indicator = canvas.transform.Find("PageIndicator");
            if (indicator != null)
                pageIndicatorText = indicator.GetComponent<Text>();
        }

        if (exitButton == null)
        {
            var exit = canvas.transform.Find("BackButton");
            if (exit == null)
                exit = canvas.transform.Find("ExitButton");
            if (exit != null)
                exitButton = exit.GetComponent<Button>();
        }

        if (storyToggleButton == null)
        {
            var toggle = canvas.transform.Find("StoryToggleButton");
            if (toggle != null)
                storyToggleButton = toggle.GetComponent<Button>();
        }

        if (storyCloseButton == null && storyReaderPanelRoot != null)
        {
            var close = storyReaderPanelRoot.Find("StoryCloseButton");
            if (close != null)
                storyCloseButton = close.GetComponent<Button>();
        }

        if (vrToggleButton == null)
        {
            var vr = canvas.transform.Find("VrToggleButton");
            if (vr != null)
                vrToggleButton = vr.GetComponent<Button>();
        }

        if (stereoToggleButton == null)
        {
            var stereo = canvas.transform.Find("StereoToggleButton");
            if (stereo != null)
                stereoToggleButton = stereo.GetComponent<Button>();
        }

        if (vrHintText == null)
        {
            var hint = canvas.transform.Find("VrHint");
            if (hint != null)
                vrHintText = hint.GetComponent<Text>();
        }

        return IsComplete;
    }

    public void EnsureStoryToggleButton()
    {
        WireFromSceneHierarchy();
        if (canvas == null)
            return;

        if (storyToggleButton != null)
            CompletedStoryRuntimeUi.ApplyStoryToggleLayout(storyToggleButton.GetComponent<RectTransform>());
        else
            storyToggleButton = CompletedStoryRuntimeUi.CreateStoryToggleButton(canvas.transform);

        if (storyCloseButton == null && storyReaderPanelRoot != null)
        {
            var close = storyReaderPanelRoot.Find("StoryCloseButton");
            if (close != null)
                storyCloseButton = close.GetComponent<Button>();
            else
                storyCloseButton = CompletedStoryRuntimeUi.CreateStoryPanelCloseButton(storyReaderPanelRoot);
        }
    }
}
