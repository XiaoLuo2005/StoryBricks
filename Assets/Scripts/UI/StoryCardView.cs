using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class StoryCardView : MonoBehaviour
{
    public Image coverImage;
    public TextMeshProUGUI titleTextTmp;
    public Button chooseButton;
    public Button cardButton;
    public Color emptyCoverColor = new Color32(230, 233, 239, 255);

    public void Bind(BrickPortfolioRoot.BrickWorkItem item, System.Action onChoose)
    {
        string title = item?.title ?? "";
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

    public void BindCompletedStory(string title, Sprite cover, System.Action onChoose, string buttonLabel = "阅读")
    {
        if (titleTextTmp != null)
            titleTextTmp.text = title ?? "";

        if (coverImage != null)
        {
            if (cover != null)
            {
                coverImage.sprite = cover;
                coverImage.color = Color.white;
            }
            else
            {
                coverImage.sprite = null;
                coverImage.color = emptyCoverColor;
            }
            coverImage.enabled = true;
        }

        if (chooseButton != null)
        {
            var label = chooseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = buttonLabel;
            chooseButton.onClick.RemoveAllListeners();
            chooseButton.onClick.AddListener(() => onChoose?.Invoke());
        }

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onChoose?.Invoke());
        }
    }
}
