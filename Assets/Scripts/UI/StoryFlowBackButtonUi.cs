using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 故事流程各页左上角导航按钮（运行时创建，风格与教程顶栏一致）。
/// </summary>
public static class StoryFlowBackButtonUi
{
    const float ButtonWidth = 200f;
    const float ButtonHeight = 72f;
    const float Margin = 28f;
    const float ButtonSpacing = 16f;

    static Font BuiltinUIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static Button EnsureTopLeft(Canvas canvas, string label, string sceneName)
    {
        return EnsureTopLeft(canvas, "BackButton", label, sceneName, 0);
    }

    public static Button EnsureTopLeft(Canvas canvas, string objectName, string label, string sceneName, int columnIndex)
    {
        return EnsureTopLeft(canvas, objectName, label, sceneName, columnIndex, preserveLayout: false);
    }

    public static Button EnsureTopLeft(
        Canvas canvas,
        string objectName,
        string label,
        string sceneName,
        int columnIndex,
        bool preserveLayout)
    {
        if (canvas == null || string.IsNullOrWhiteSpace(sceneName))
            return null;

        var canvasRt = canvas.GetComponent<RectTransform>();
        var existing = canvasRt.Find(objectName);
        if (existing != null)
        {
            var existingBtn = existing.GetComponent<Button>();
            if (existingBtn != null)
            {
                if (!preserveLayout)
                    LayoutTopLeft(existing.GetComponent<RectTransform>(), columnIndex);
                Wire(existingBtn, label, sceneName);
                existing.SetAsLastSibling();
                return existingBtn;
            }
        }

        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRt, false);
        LayoutTopLeft(rt, columnIndex);
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        StretchFull(textRt);
        var text = textGo.AddComponent<Text>();
        text.font = BuiltinUIFont;
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(40, 44, 52, 255);
        text.text = label;

        go.transform.SetAsLastSibling();
        Wire(btn, label, sceneName);
        return btn;
    }

    public static Button EnsureTopRight(Canvas canvas, string label, string sceneName)
    {
        return EnsureTopRight(canvas, "ExitButton", label, sceneName);
    }

    public static Button EnsureTopRight(Canvas canvas, string objectName, string label, string sceneName)
    {
        if (canvas == null || string.IsNullOrWhiteSpace(sceneName))
            return null;

        var canvasRt = canvas.GetComponent<RectTransform>();
        var existing = canvasRt.Find(objectName);
        if (existing != null)
        {
            var existingBtn = existing.GetComponent<Button>();
            if (existingBtn != null)
            {
                LayoutTopRight(existing.GetComponent<RectTransform>());
                Wire(existingBtn, label, sceneName);
                existing.SetAsLastSibling();
                return existingBtn;
            }
        }

        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRt, false);
        LayoutTopRight(rt);
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        StretchFull(textRt);
        var text = textGo.AddComponent<Text>();
        text.font = BuiltinUIFont;
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(40, 44, 52, 255);
        text.text = label;

        go.transform.SetAsLastSibling();
        Wire(btn, label, sceneName);
        return btn;
    }

    static void LayoutTopLeft(RectTransform rt, int columnIndex)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(
            Margin + columnIndex * (ButtonWidth + ButtonSpacing),
            -Margin);
    }

    static void LayoutTopRight(RectTransform rt)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-Margin, -Margin);
    }

    static void Wire(Button btn, string label, string sceneName)
    {
        BindNavigation(btn, label, sceneName);
    }

    public static void BindNavigation(Button btn, string label, string sceneName)
    {
        if (btn == null || string.IsNullOrWhiteSpace(sceneName))
            return;

        var text = btn.GetComponentInChildren<Text>();
        if (text != null)
            text.text = label;

        var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = label;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => SceneManager.LoadScene(sceneName.Trim()));
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
