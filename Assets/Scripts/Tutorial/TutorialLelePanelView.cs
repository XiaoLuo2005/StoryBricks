using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 乐乐语音面板 UI 引用。在 TutorialStepsPage Prefab 里可视化摆放各子节点位置。
/// </summary>
[DisallowMultipleComponent]
public class TutorialLelePanelView : MonoBehaviour
{
    public Image panelBackground;
    public TextMeshProUGUI titleText;
    public ScrollRect dialogScroll;
    public TextMeshProUGUI dialogOutput;
    public TextMeshProUGUI listenStatusLabel;
    public TextMeshProUGUI statusText;

    TutorialLeleDialogLog _dialogLog;

    public TutorialLeleDialogLog DialogLog
    {
        get
        {
            EnsureDialogLog();
            return _dialogLog;
        }
    }

    public bool IsComplete =>
        dialogScroll != null &&
        listenStatusLabel != null &&
        statusText != null;

    public void EnsureDialogLog()
    {
        if (dialogScroll == null)
            return;

        if (panelBackground != null)
            panelBackground.raycastTarget = false;

        if (dialogOutput == null && dialogScroll.content != null)
            dialogOutput = dialogScroll.content.Find("Output")?.GetComponent<TextMeshProUGUI>();

        if (_dialogLog == null)
        {
            _dialogLog = GetComponent<TutorialLeleDialogLog>();
            if (_dialogLog == null)
                _dialogLog = gameObject.AddComponent<TutorialLeleDialogLog>();
        }

        _dialogLog.Initialize(dialogScroll, TutorialUiArt.Font, dialogOutput);
        if (dialogOutput == null && dialogScroll.content != null)
            dialogOutput = dialogScroll.content.Find("Output")?.GetComponent<TextMeshProUGUI>();
    }

    public void ScrollDialogToLatest()
    {
        EnsureDialogLog();
        _dialogLog?.ScrollToLatestIfNeeded();
    }

    void Start()
    {
        StartCoroutine(EnsureDefaultOpening());
    }

    IEnumerator EnsureDefaultOpening()
    {
        yield return null;
        EnsureDialogLog();
        if (_dialogLog != null && !_dialogLog.HasMessages)
        {
            _dialogLog.SetOpening(
                $"你好！我是{LeleVoiceAssistant.DisplayName}。直接说话提问就行，我会根据当前步骤帮你讲解。");
        }
    }
}
