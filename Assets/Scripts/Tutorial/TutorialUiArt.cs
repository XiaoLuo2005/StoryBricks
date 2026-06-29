using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>教程页 UI 美术：背景、按钮、乐乐面板与 word SDF 字体（Resources/TutorialUi）。</summary>
public static class TutorialUiArt
{
    const string BackgroundResourcePath = "TutorialUi/Background";
    const string ButtonResourcePath = "TutorialUi/Button";
    const string LelePanelResourcePath = "TutorialUi/LelePanelBackground";
    const string FontResourcePath = "TutorialUi/word SDF";
    const string FallbackFontResourcePath = "UI/word SDF";

    static Sprite _background;
    static Sprite _button;
    static Sprite _lelePanelBackground;
    static TMP_FontAsset _font;

    public static readonly Color TitleBrown = new Color(0.35f, 0.18f, 0.08f, 1f);
    public static readonly Color BodyBrown = new Color(0.28f, 0.16f, 0.08f, 1f);
    public static readonly Color MutedBrown = new Color(0.45f, 0.32f, 0.22f, 1f);
    public static readonly Color UserBubbleBg = new Color(0.78f, 0.88f, 0.98f, 0.92f);
    public static readonly Color LeleBubbleBg = new Color(1f, 0.95f, 0.86f, 0.95f);
    public static readonly Color SystemBubbleBg = new Color(0.94f, 0.90f, 0.84f, 0.55f);
    public static readonly Color UserRoleColor = new Color(0.14f, 0.39f, 0.75f, 1f);
    public static readonly Color LeleRoleColor = new Color(0.55f, 0.32f, 0.12f, 1f);
    public static readonly Color SystemTextColor = new Color(0.42f, 0.34f, 0.28f, 1f);

    public static Sprite Background => _background ??= Resources.Load<Sprite>(BackgroundResourcePath);
    public static Sprite Button => _button ??= Resources.Load<Sprite>(ButtonResourcePath);
    public static Sprite LelePanelBackground =>
        _lelePanelBackground ??= Resources.Load<Sprite>(LelePanelResourcePath);

    public static TMP_FontAsset Font =>
        _font ??= Resources.Load<TMP_FontAsset>(FontResourcePath)
                  ?? Resources.Load<TMP_FontAsset>(FallbackFontResourcePath);

    public static void ApplyBackground(Image image)
    {
        if (image == null)
            return;

        var sprite = Background;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }
        else
        {
            image.sprite = null;
            image.color = new Color32(248, 249, 252, 255);
        }

        image.raycastTarget = false;
    }

    public static void ApplyButtonBackground(Image image)
    {
        if (image == null)
            return;

        var sprite = Button;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.sprite = null;
            image.color = new Color32(255, 220, 120, 255);
        }
    }

    public static void ApplyLelePanelBackground(Image image)
    {
        if (image == null)
            return;

        var sprite = LelePanelBackground;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.sprite = null;
            image.color = new Color32(255, 248, 230, 245);
        }
    }

    public static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string content,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        var font = Font;
        if (font != null)
            tmp.font = font;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        return tmp;
    }
}
