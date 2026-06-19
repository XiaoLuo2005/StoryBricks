using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>我的故事 / 绘本浏览场景的运行时 UI 搭建辅助。</summary>
public static class CompletedStoryRuntimeUi
{
    public static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    public static Canvas CreateOverlayCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        return canvas;
    }

    public static GameObject CreateCenterPanel(Transform parent, string message, Font font)
    {
        var go = CreateUiObject(parent, "EmptyPanel");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 280f);
        rt.anchoredPosition = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color32(255, 255, 255, 245);

        var textGo = CreateUiObject(go.transform, "Hint");
        StretchFull(textGo.GetComponent<RectTransform>());
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.offsetMin = new Vector2(32f, 32f);
        textRt.offsetMax = new Vector2(-32f, -32f);

        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.fontSize = 32;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(70, 76, 90, 255);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = message;
        return go;
    }

    public static void EnsureCanvasScaler(Canvas canvas)
    {
        if (canvas == null)
            return;

        var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static Text CreateHeader(Transform parent, string title, Font font)
    {
        var go = CreateUiObject(parent, "HeaderTitle");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, 96f);
        rt.anchoredPosition = new Vector2(0f, -120f);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 48;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(40, 44, 52, 255);
        text.text = title;
        return text;
    }

    public static ScrollRect CreateScrollView(Transform parent, out RectTransform content)
    {
        var root = CreateUiObject(parent, "ScrollView");
        StretchWithInsets(root.GetComponent<RectTransform>(), 48f, 180f, 48f, 48f);

        var viewport = CreateUiObject(root.transform, "Viewport");
        StretchFull(viewport.GetComponent<RectTransform>());
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color32(245, 247, 250, 255);
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGo = CreateUiObject(viewport.transform, "Content");
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(320f, 380f);
        grid.spacing = new Vector2(32f, 32f);
        grid.padding = new RectOffset(24, 24, 24, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return scroll;
    }

    public static void CreateCenterHint(RectTransform parent, string message, Font font)
    {
        var go = CreateUiObject(parent, "EmptyHint");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, 240f);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 30;
        text.alignment = TextAnchor.UpperCenter;
        text.color = new Color32(90, 96, 110, 255);
        text.text = message;
    }

    public sealed class StoryCardRefs
    {
        public Image coverImage;
        public Button button;
    }

    public static StoryCardRefs CreateStoryCard(Transform parent, Font font, string title, int pageCount)
    {
        var root = CreateUiObject(parent, "StoryCard");
        var rootImg = root.AddComponent<Image>();
        rootImg.color = Color.white;
        var button = root.AddComponent<Button>();
        button.targetGraphic = rootImg;

        var coverGo = CreateUiObject(root.transform, "Cover");
        SetAnchors(coverGo.GetComponent<RectTransform>(), new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.92f));
        var coverImg = coverGo.AddComponent<Image>();
        coverImg.color = new Color32(230, 233, 239, 255);
        coverImg.preserveAspect = true;
        coverImg.raycastTarget = false;

        var titleGo = CreateUiObject(root.transform, "Title");
        SetAnchors(titleGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.33f));
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 24;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color32(40, 44, 52, 255);
        titleText.text = title;
        titleText.raycastTarget = false;

        var metaGo = CreateUiObject(root.transform, "Meta");
        SetAnchors(metaGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.21f));
        var metaText = metaGo.AddComponent<Text>();
        metaText.font = font;
        metaText.fontSize = 20;
        metaText.alignment = TextAnchor.MiddleCenter;
        metaText.color = new Color32(110, 118, 130, 255);
        metaText.text = $"{pageCount} 页 · 点击阅读";
        metaText.raycastTarget = false;

        var actionGo = CreateUiObject(root.transform, "OpenLabel");
        SetAnchors(actionGo.GetComponent<RectTransform>(), new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.11f));
        var actionBg = actionGo.AddComponent<Image>();
        actionBg.color = new Color32(66, 133, 244, 255);
        actionBg.raycastTarget = false;

        var labelGo = CreateUiObject(actionGo.transform, "Label");
        StretchFull(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "阅读";
        label.raycastTarget = false;

        return new StoryCardRefs { coverImage = coverImg, button = button };
    }

    public static Image CreateFullScreenImage(Transform parent, string name)
    {
        var go = CreateUiObject(parent, name);
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.preserveAspect = false;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    public static Text CreateBottomCaption(Transform parent, Font font)
    {
        var go = CreateUiObject(parent, "Caption");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-128f, 160f);
        rt.anchoredPosition = new Vector2(0f, 280f);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 34;
        text.alignment = TextAnchor.LowerLeft;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color32(0, 0, 0, 180);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    public static (Button prev, Button next, Text indicator) CreateBottomNav(
        Transform parent,
        Font font,
        Vector2 buttonSize,
        float edgePadding,
        float bottomInset,
        float buttonSpacing)
    {
        var prev = CreateNavButton(parent, "PrevPageButton", "上一页", font, buttonSize, edgePadding, bottomInset, false, buttonSpacing);
        var next = CreateNavButton(parent, "NextPageButton", "下一页", font, buttonSize, edgePadding, bottomInset, false, buttonSpacing * 2f + buttonSize.x);

        var indicatorGo = CreateUiObject(parent, "PageIndicator");
        var indicatorRt = indicatorGo.GetComponent<RectTransform>();
        indicatorRt.anchorMin = new Vector2(1f, 0f);
        indicatorRt.anchorMax = new Vector2(1f, 0f);
        indicatorRt.pivot = new Vector2(1f, 0f);
        indicatorRt.sizeDelta = new Vector2(420f, 72f);
        indicatorRt.anchoredPosition = new Vector2(-edgePadding, bottomInset + (buttonSize.y - 72f) * 0.5f);

        var indicator = indicatorGo.AddComponent<Text>();
        indicator.font = font;
        indicator.fontSize = 28;
        indicator.fontStyle = FontStyle.Bold;
        indicator.alignment = TextAnchor.MiddleRight;
        indicator.color = Color.white;
        var outline = indicatorGo.AddComponent<Outline>();
        outline.effectColor = new Color32(40, 40, 40, 200);
        outline.effectDistance = new Vector2(1f, -1f);

        prev.transform.SetAsLastSibling();
        next.transform.SetAsLastSibling();
        indicatorGo.transform.SetAsLastSibling();

        return (prev, next, indicator);
    }

    static Button CreateNavButton(
        Transform parent,
        string name,
        string label,
        Font font,
        Vector2 size,
        float inset,
        float bottomInset,
        bool alignRight,
        float xOffset)
    {
        var go = CreateUiObject(parent, name);
        var rt = go.GetComponent<RectTransform>();
        float xAnchor = alignRight ? 1f : 0f;
        rt.anchorMin = new Vector2(xAnchor, 0f);
        rt.anchorMax = new Vector2(xAnchor, 0f);
        rt.pivot = new Vector2(xAnchor, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(alignRight ? -inset : inset + xOffset, bottomInset);

        var img = go.AddComponent<Image>();
        img.color = new Color32(30, 30, 30, 170);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var labelGo = CreateUiObject(go.transform, "Label");
        StretchFull(labelGo.GetComponent<RectTransform>());
        var text = labelGo.AddComponent<Text>();
        text.font = font;
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        return button;
    }

    static GameObject CreateUiObject(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchWithInsets(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
