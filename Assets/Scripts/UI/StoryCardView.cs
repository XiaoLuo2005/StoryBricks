using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class StoryCardView : MonoBehaviour
{
    public Image coverImage;
    [Tooltip("Legacy UI Text，与 Title Text Tmp 二选一")]
    public Text titleText;
    public TextMeshProUGUI titleTextTmp;
    public Button chooseButton;
    public Button cardButton;
    public Color emptyCoverColor = new Color32(230, 233, 239, 255);

    public void Bind(BrickPortfolioRoot.BrickWorkItem item, System.Action onChoose)
    {
        string title = item?.title ?? "";
        if (titleText != null)
            titleText.text = title;
        if (titleTextTmp != null)
            titleTextTmp.text = title;

        if (coverImage != null)
        {
            if (item != null && item.thumbnail != null)
            {
                coverImage.sprite = item.thumbnail;
                coverImage.color = Color.white;
            }
            else
            {
                coverImage.sprite = null;
                coverImage.color = emptyCoverColor;
            }
            coverImage.enabled = true;
        }

        void Wire(Button b)
        {
            if (b == null)
                return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => onChoose?.Invoke());
        }

        Wire(chooseButton);
        Wire(cardButton);
    }
}
