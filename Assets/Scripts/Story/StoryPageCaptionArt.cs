using TMPro;
using UnityEngine;

/// <summary>创作页 / 绘本页固定区域故事文案：字体与字数限制。</summary>
public static class StoryPageCaptionArt
{
    public const string PreferredFontResourcePath = "UI/word SDF";
    public const int DefaultMaxChars = 120;

    static TMP_FontAsset _cachedFont;

    public static TMP_FontAsset ResolveFont(TMP_FontAsset assigned = null)
    {
        if (assigned != null)
            return assigned;

        if (_cachedFont != null)
            return _cachedFont;

        _cachedFont = Resources.Load<TMP_FontAsset>(PreferredFontResourcePath)
                      ?? Resources.Load<TMP_FontAsset>("UI/word SDF")
                      ?? Resources.Load<TMP_FontAsset>("TutorialUi/word SDF");
        return _cachedFont;
    }

    public static string Clamp(string text, int maxChars = DefaultMaxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var t = text.Trim().Replace("\r\n", " ").Replace('\n', ' ');
        while (t.Contains("  "))
            t = t.Replace("  ", " ");

        if (maxChars <= 0 || t.Length <= maxChars)
            return t;

        if (maxChars <= 1)
            return "…";

        return t.Substring(0, maxChars - 1).TrimEnd('，', '。', '、', ' ') + "…";
    }

    public static string FallbackFromScene(string sceneGuide, string pageTitle, int maxChars = DefaultMaxChars)
    {
        string raw = !string.IsNullOrWhiteSpace(sceneGuide)
            ? sceneGuide.Trim()
            : (!string.IsNullOrWhiteSpace(pageTitle) ? $"{pageTitle}的故事开始了。" : "这一页的故事开始了。");
        return Clamp(raw, maxChars);
    }

    public static void ApplyCaptionStyle(TextMeshProUGUI tmp, TMP_FontAsset font, int maxChars)
    {
        if (tmp == null)
            return;

        var resolved = ResolveFont(font);
        if (resolved != null)
            tmp.font = resolved;

        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.fontSize = 32f;
        tmp.lineSpacing = 4f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = new Color32(255, 252, 245, 255);
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color32(40, 28, 18, 200);
        tmp.characterSpacing = 0.5f;
    }
}
