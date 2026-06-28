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

    public bool IsComplete =>
        dialogOutput != null &&
        listenStatusLabel != null &&
        statusText != null;
}
