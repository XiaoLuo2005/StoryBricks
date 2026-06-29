using TMPro;
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

    public sealed class StoryReaderPanelRefs
    {
        public RectTransform root;
        public TextMeshProUGUI storyText;
        public Button recordButton;
        public Button playButton;
        public Button rerecordButton;
        public Text statusText;
        public Button closeButton;
    }

    public const float StoryReaderPanelBottom = 300f;
    public const float StoryReaderPanelHeight = 300f;
    public const float StoryReaderPanelLeft = 48f;
    public const float StoryToggleGap = 12f;

    public static StoryReaderPanelRefs CreateStoryReaderPanel(Transform parent)
    {
        var rootGo = CreateUiObject(parent, "StoryReaderPanel");
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 0f);
        rootRt.anchorMax = new Vector2(0f, 0f);
        rootRt.pivot = new Vector2(0f, 0f);
        rootRt.sizeDelta = new Vector2(640f, StoryReaderPanelHeight);
        rootRt.anchoredPosition = new Vector2(StoryReaderPanelLeft, StoryReaderPanelBottom);

        var bg = rootGo.AddComponent<Image>();
        bg.color = new Color32(255, 252, 245, 220);
        bg.raycastTarget = true;

        var closeButton = CreateStoryPanelCloseButton(rootGo.transform);

        var storyText = CreateScrollableStoryText(
            rootGo.transform,
            "StoryTextScroll",
            "StoryText",
            0f,
            0.34f,
            1f,
            1f,
            new Vector2(20f, 0f),
            new Vector2(-20f, -44f));
        StoryPageCaptionArt.ApplyReaderCaptionStyle(storyText, StoryPageCaptionArt.ResolveFont(null));
        storyText.text = "";

        var rowGo = CreateUiObject(rootGo.transform, "VoiceRow");
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(0.5f, 0f);
        rowRt.sizeDelta = new Vector2(-24f, 88f);
        rowRt.anchoredPosition = new Vector2(0f, 12f);

        var recordButton = CreateSmallActionButton(rowGo.transform, "RecordButton", "录音", 0f);
        var playButton = CreateSmallActionButton(rowGo.transform, "PlayButton", "播放", 136f);
        var rerecordButton = CreateSmallActionButton(rowGo.transform, "RerecordButton", "重录", 272f);

        var statusGo = CreateUiObject(rowGo.transform, "Status");
        var statusRt = statusGo.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 0f);
        statusRt.anchorMax = new Vector2(1f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.sizeDelta = new Vector2(0f, 28f);
        statusRt.anchoredPosition = new Vector2(0f, 52f);
        var statusText = statusGo.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 20;
        statusText.alignment = TextAnchor.MiddleLeft;
        statusText.color = StoryPageCaptionArt.BodyBrownColor;
        statusText.text = "让我们一起阅读吧~";

        return new StoryReaderPanelRefs
        {
            root = rootRt,
            storyText = storyText,
            recordButton = recordButton,
            playButton = playButton,
            rerecordButton = rerecordButton,
            statusText = statusText,
            closeButton = closeButton,
        };
    }

    public static Button CreateStoryPanelCloseButton(Transform panelRoot, string label = "收起")
    {
        var go = CreateUiObject(panelRoot, "StoryCloseButton");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(96f, 40f);
        rt.anchoredPosition = new Vector2(-10f, -10f);

        var img = go.AddComponent<Image>();
        img.color = new Color32(255, 255, 255, 235);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var labelGo = CreateUiObject(go.transform, "Label");
        StretchFull(labelGo.GetComponent<RectTransform>());
        var text = labelGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = StoryPageCaptionArt.BodyBrownColor;
        text.text = label;
        return button;
    }

    public static Vector2 GetStoryToggleAnchoredPosition()
    {
        return new Vector2(
            StoryReaderPanelLeft,
            StoryReaderPanelBottom + StoryReaderPanelHeight + StoryToggleGap);
    }

    public static void ApplyStoryToggleLayout(RectTransform toggleRt)
    {
        if (toggleRt == null)
            return;

        toggleRt.anchorMin = new Vector2(0f, 0f);
        toggleRt.anchorMax = new Vector2(0f, 0f);
        toggleRt.pivot = new Vector2(0f, 0f);
        toggleRt.anchoredPosition = GetStoryToggleAnchoredPosition();
    }

    public static Button CreateStoryToggleButton(Transform parent, string label = "故事阅读")
    {
        var go = CreateUiObject(parent, "StoryToggleButton");
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(168f, 64f);
        ApplyStoryToggleLayout(rt);

        var img = go.AddComponent<Image>();
        img.color = new Color32(255, 252, 245, 235);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var labelGo = CreateUiObject(go.transform, "Label");
        StretchFull(labelGo.GetComponent<RectTransform>());
        var text = labelGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = StoryPageCaptionArt.BodyBrownColor;
        text.text = label;
        return button;
    }

    public static void SetStoryToggleLabel(Button button, string label)
    {
        if (button == null)
            return;
        var text = button.GetComponentInChildren<Text>();
        if (text != null)
            text.text = label ?? "";
    }

    public static TextMeshProUGUI ResolveScrollableStoryText(
        Transform panelRoot,
        string scrollName,
        string textName)
    {
        if (panelRoot == null)
            return null;

        var scroll = panelRoot.Find(scrollName);
        if (scroll != null)
        {
            var nested = scroll.Find($"Viewport/Content/{textName}");
            if (nested != null)
                return nested.GetComponent<TextMeshProUGUI>();

            nested = scroll.Find(textName);
            if (nested != null)
                return nested.GetComponent<TextMeshProUGUI>();
        }

        var direct = panelRoot.Find(textName);
        return direct != null ? direct.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>修正手动搭建的 ScrollRect，使文字在框内换行并被 Viewport 裁剪。</summary>
    public static void EnsureScrollableStoryTextLayout(TextMeshProUGUI tmp)
    {
        if (tmp == null)
            return;

        var scroll = tmp.GetComponentInParent<ScrollRect>();
        if (scroll == null)
            return;

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = scroll.viewport;
        if (viewport == null)
        {
            var viewportTf = scroll.transform.Find("Viewport");
            if (viewportTf != null)
                viewport = viewportTf.GetComponent<RectTransform>();
        }

        if (viewport == null)
            return;

        StretchFull(viewport);
        EnsureViewportClipping(viewport);
        scroll.viewport = viewport;

        var content = scroll.content;
        if (content == null)
        {
            var contentTf = viewport.Find("Content");
            if (contentTf == null)
            {
                var contentGo = CreateUiObject(viewport, "Content");
                content = contentGo.GetComponent<RectTransform>();
            }
            else
            {
                content = contentTf.GetComponent<RectTransform>();
            }
        }

        if (tmp.transform.parent != content)
            tmp.transform.SetParent(content, false);

        scroll.content = content;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, content.offsetMax.y);

        var contentFitter = content.GetComponent<ContentSizeFitter>();
        if (contentFitter == null)
            contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textRt = tmp.rectTransform;
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.offsetMin = new Vector2(0f, textRt.offsetMin.y);
        textRt.offsetMax = new Vector2(0f, textRt.offsetMax.y);

        var textFitter = textRt.GetComponent<ContentSizeFitter>();
        if (textFitter == null)
            textFitter = textRt.gameObject.AddComponent<ContentSizeFitter>();
        textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textLayout = textRt.GetComponent<LayoutElement>();
        if (textLayout != null)
        {
            textLayout.minWidth = -1f;
            textLayout.preferredWidth = -1f;
            textLayout.flexibleWidth = -1f;
        }

        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        Canvas.ForceUpdateCanvases();

        var viewportWidth = viewport.rect.width;
        if (viewportWidth > 1f)
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);

        tmp.ForceMeshUpdate(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    static void EnsureViewportClipping(RectTransform viewport)
    {
        var legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
            legacyMask.enabled = false;

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();
    }

    public static TextMeshProUGUI CreateScrollableStoryText(
        Transform parent,
        string scrollObjectName,
        string textObjectName,
        float anchorMinX,
        float anchorMinY,
        float anchorMaxX,
        float anchorMaxY,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var scrollGo = CreateUiObject(parent, scrollObjectName);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(anchorMinX, anchorMinY);
        scrollRt.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        scrollRt.offsetMin = offsetMin;
        scrollRt.offsetMax = offsetMax;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var viewport = CreateUiObject(scrollGo.transform, "Viewport");
        StretchFull(viewport.GetComponent<RectTransform>());
        var viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImg.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        var contentGo = CreateUiObject(viewport.transform, "Content");
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = CreateUiObject(contentGo.transform, textObjectName);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = new Vector2(0f, 0f);

        var textFitter = textGo.AddComponent<ContentSizeFitter>();
        textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRt;

        return tmp;
    }

    public static void ResetStoryTextScroll(TextMeshProUGUI tmp)
    {
        if (tmp == null)
            return;

        EnsureScrollableStoryTextLayout(tmp);

        var scroll = tmp.GetComponentInParent<ScrollRect>();
        if (scroll == null)
            return;

        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
    }

    static Button CreateSmallActionButton(Transform parent, string name, string label, float x)
    {
        var go = CreateUiObject(parent, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(120f, 44f);
        rt.anchoredPosition = new Vector2(x, 0f);

        var img = go.AddComponent<Image>();
        img.color = new Color32(255, 255, 255, 235);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var labelGo = CreateUiObject(go.transform, "Label");
        StretchFull(labelGo.GetComponent<RectTransform>());
        var text = labelGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = StoryPageCaptionArt.BodyBrownColor;
        text.text = label;
        return button;
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
