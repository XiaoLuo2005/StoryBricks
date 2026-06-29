using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>生成带 StoryCreationPageView 的 Canvas，供 Prefab / 场景可视化编辑。</summary>
public static class StoryCreationPageUiBuilder
{
    public const float EdgePadding = 48f;
    public const float BottomInset = 56f;
    public const float TopStatusBarOffset = 108f;
    public const float ButtonSpacing = 24f;
    public const float FloatingButtonHeight = 72f;
    public static readonly Vector2 ActionButtonSize = new Vector2(160f, 72f);
    public static readonly Vector2 PrimaryButtonSize = new Vector2(200f, 80f);
    public const float CameraPreviewMiniWidth = 280f;
    public const float CameraPreviewMiniHeight = 158f;
    public const float CameraPreviewMargin = 28f;

    public static Font BuiltinUIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static StoryCreationPageView BuildPageView(Transform parent)
    {
        var canvasGo = new GameObject("StoryCreationCanvas", typeof(RectTransform));
        SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));
        if (parent != null)
            canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var view = canvasGo.AddComponent<StoryCreationPageView>();
        view.canvas = canvas;
        view.canvasScaler = scaler;

        var root = canvasGo.GetComponent<RectTransform>();
        StretchFull(root);

        view.backgroundImage = CreateUiObject<Image>(root, "Background");
        StretchFull(view.backgroundImage.rectTransform);
        view.backgroundImage.color = Color.white;
        view.backgroundImage.preserveAspect = false;
        view.backgroundImage.raycastTarget = false;

        view.generatedPageImage = CreateUiObject<RawImage>(root, "GeneratedPage");
        StretchFull(view.generatedPageImage.rectTransform);
        view.generatedPageImage.color = Color.white;
        view.generatedPageImage.raycastTarget = false;
        view.generatedPageImage.gameObject.SetActive(false);

        view.backButton = CreateBackButton(root);

        view.pageIndicatorText = CreateOverlayText(root, "PageIndicator", "", 28, TextAnchor.UpperRight);
        var indicatorRt = view.pageIndicatorText.rectTransform;
        indicatorRt.anchorMin = new Vector2(1f, 1f);
        indicatorRt.anchorMax = new Vector2(1f, 1f);
        indicatorRt.pivot = new Vector2(1f, 1f);
        indicatorRt.sizeDelta = new Vector2(360f, 48f);
        indicatorRt.anchoredPosition = new Vector2(-EdgePadding, -96f);

        view.guideText = CreateOverlayText(root, "GuideText", "", 28, TextAnchor.LowerLeft);
        var guideRt = view.guideText.rectTransform;
        guideRt.anchorMin = new Vector2(0f, 0f);
        guideRt.anchorMax = new Vector2(0.62f, 0f);
        guideRt.pivot = new Vector2(0f, 0f);
        guideRt.sizeDelta = new Vector2(0f, 120f);
        guideRt.anchoredPosition = new Vector2(EdgePadding, BottomInset + FloatingButtonHeight + 12f);

        view.voiceGuideButton = CreateFloatingButton(root, "VoiceGuideButton", "播放引导", ActionButtonSize, false);
        view.rebuildButton = CreateFloatingButton(root, "RebuildButton", "重搭", new Vector2(120f, 72f), false);
        view.confirmButton = CreateFloatingButton(root, "ConfirmButton", "这页摆好了", PrimaryButtonSize, true);
        view.regenerateButton = CreateFloatingButton(root, "RegenerateButton", "重讲", new Vector2(120f, 72f), false);
        view.nextPageButton = CreateFloatingButton(root, "NextPageButton", "下一页", PrimaryButtonSize, true);

        LayoutFloatingButton(view.rebuildButton, EdgePadding, false, new Vector2(120f, 72f));
        LayoutFloatingButton(view.confirmButton, EdgePadding + 120f + ButtonSpacing, false, PrimaryButtonSize);
        LayoutFloatingButton(view.regenerateButton, EdgePadding, true, new Vector2(120f, 72f));
        LayoutFloatingButton(
            view.nextPageButton,
            EdgePadding + 120f + ButtonSpacing,
            true,
            PrimaryButtonSize);
        if (view.voiceGuideButton != null)
            view.voiceGuideButton.gameObject.SetActive(false);

        BuildAnswerInputUi(view, root);
        BuildStatusPanel(view, root);
        BuildCameraPreviewUi(view, root);
        BuildPageCaptionPanel(view, root);
        BuildStoryToggleButton(view, root);

        view.backgroundImage.transform.SetAsFirstSibling();
        if (view.statusPanel != null)
            view.statusPanel.transform.SetAsLastSibling();
        if (view.cameraPreviewOverlay != null)
            view.cameraPreviewOverlay.transform.SetAsLastSibling();
        if (view.pageCaptionPanel != null)
            view.pageCaptionPanel.transform.SetAsLastSibling();

        return view;
    }

    static void BuildPageCaptionPanel(StoryCreationPageView view, RectTransform root)
    {
        view.pageCaptionPanel = new GameObject("PageCaptionPanel", typeof(RectTransform));
        view.pageCaptionPanel.layer = LayerMask.NameToLayer("UI");
        var panelRt = view.pageCaptionPanel.GetComponent<RectTransform>();
        panelRt.SetParent(root, false);
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.sizeDelta = new Vector2(580f, 168f);
        panelRt.anchoredPosition = new Vector2(EdgePadding, BottomInset + PrimaryButtonSize.y + 20f);

        var bg = view.pageCaptionPanel.AddComponent<Image>();
        bg.color = new Color32(255, 252, 245, 210);
        bg.raycastTarget = false;

        view.pageCaptionText = CompletedStoryRuntimeUi.CreateScrollableStoryText(
            panelRt,
            "CaptionScroll",
            "CaptionText",
            0f,
            0f,
            1f,
            1f,
            new Vector2(16f, 12f),
            new Vector2(-16f, -44f));
        view.pageCaptionFont = StoryPageCaptionArt.ResolveFont(view.pageCaptionFont);
        StoryPageCaptionArt.ApplyScrollableStoryTextStyle(
            view.pageCaptionText,
            view.pageCaptionFont,
            30f,
            TextAlignmentOptions.TopLeft);
        view.pageCaptionText.text = "";
        view.storyCloseButton = CompletedStoryRuntimeUi.CreateStoryPanelCloseButton(panelRt);
        view.pageCaptionPanel.SetActive(true);
    }

    static void BuildStoryToggleButton(StoryCreationPageView view, RectTransform root)
    {
        float panelBottom = BottomInset + PrimaryButtonSize.y + 20f;
        const float panelHeight = 168f;

        view.storyToggleButton = CreateFloatingButton(
            root,
            "StoryToggleButton",
            "故事阅读",
            new Vector2(168f, 64f),
            false);
        var rt = view.storyToggleButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(
            EdgePadding,
            panelBottom + panelHeight + 12f);
    }

    static Button CreateBackButton(RectTransform root)
    {
        var go = new GameObject("BackButton", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -28f);
        rt.sizeDelta = new Vector2(200f, 72f);

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
        text.text = "← 返回作品集";

        return btn;
    }

    static void BuildStatusPanel(StoryCreationPageView view, RectTransform root)
    {
        view.statusPanel = new GameObject("StatusPanel", typeof(RectTransform));
        view.statusPanel.layer = LayerMask.NameToLayer("UI");
        var statusPanelRt = view.statusPanel.GetComponent<RectTransform>();
        statusPanelRt.SetParent(root, false);
        statusPanelRt.anchorMin = new Vector2(0.18f, 1f);
        statusPanelRt.anchorMax = new Vector2(0.82f, 1f);
        statusPanelRt.pivot = new Vector2(0.5f, 1f);
        statusPanelRt.sizeDelta = new Vector2(0f, 52f);
        statusPanelRt.anchoredPosition = new Vector2(0f, -TopStatusBarOffset);

        var statusBg = view.statusPanel.AddComponent<Image>();
        statusBg.color = new Color32(20, 24, 32, 170);
        statusBg.raycastTarget = false;

        view.statusText = CreateOverlayText(view.statusPanel.transform, "StatusText", "", 26, TextAnchor.MiddleCenter);
        StretchFull(view.statusText.rectTransform);
        view.statusText.alignment = TextAnchor.MiddleCenter;
        view.statusPanel.SetActive(false);
    }

    static void BuildAnswerInputUi(StoryCreationPageView view, RectTransform root)
    {
        view.answerUiRoot = new GameObject("AnswerInputRoot", typeof(RectTransform));
        view.answerUiRoot.layer = LayerMask.NameToLayer("UI");
        var rootRt = view.answerUiRoot.GetComponent<RectTransform>();
        rootRt.SetParent(root, false);
        rootRt.anchorMin = new Vector2(1f, 0f);
        rootRt.anchorMax = new Vector2(1f, 0f);
        rootRt.pivot = new Vector2(1f, 0f);
        rootRt.sizeDelta = new Vector2(520f, 200f);
        rootRt.anchoredPosition = new Vector2(-EdgePadding, BottomInset + PrimaryButtonSize.y + ButtonSpacing + 8f);
        view.answerUiRoot.SetActive(false);

        view.answerModeVoiceButton = CreateFloatingButton(
            rootRt,
            "AnswerModeVoice",
            "语音",
            new Vector2(100f, 48f),
            false);
        LayoutChildButton(view.answerModeVoiceButton, 0f, 152f, new Vector2(100f, 48f));

        view.answerModeTextButton = CreateFloatingButton(
            rootRt,
            "AnswerModeText",
            "文字",
            new Vector2(100f, 48f),
            false);
        LayoutChildButton(view.answerModeTextButton, 108f, 152f, new Vector2(100f, 48f));

        view.answerVoicePanel = new GameObject("AnswerVoicePanel", typeof(RectTransform));
        view.answerVoicePanel.layer = LayerMask.NameToLayer("UI");
        var voicePanelRt = view.answerVoicePanel.GetComponent<RectTransform>();
        voicePanelRt.SetParent(rootRt, false);
        StretchFull(voicePanelRt);

        view.answerVoiceButton = CreateFloatingButton(
            voicePanelRt,
            "AnswerVoiceButton",
            LeleVoiceAssistant.WakeHint,
            PrimaryButtonSize,
            true);
        var vBtnRt = view.answerVoiceButton.GetComponent<RectTransform>();
        vBtnRt.anchorMin = new Vector2(1f, 0f);
        vBtnRt.anchorMax = new Vector2(1f, 0f);
        vBtnRt.pivot = new Vector2(1f, 0f);
        vBtnRt.anchoredPosition = Vector2.zero;
        ConfigureAnswerVoiceIndicator(view.answerVoiceButton);
        view.answerVoiceButton.gameObject.SetActive(false);

        view.answerTextPanel = new GameObject("AnswerTextPanel", typeof(RectTransform));
        view.answerTextPanel.layer = LayerMask.NameToLayer("UI");
        var textPanelRt = view.answerTextPanel.GetComponent<RectTransform>();
        textPanelRt.SetParent(rootRt, false);
        StretchFull(textPanelRt);

        var inputGo = new GameObject("AnswerTextInput", typeof(RectTransform));
        inputGo.layer = LayerMask.NameToLayer("UI");
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.SetParent(textPanelRt, false);
        inputRt.anchorMin = new Vector2(0f, 0f);
        inputRt.anchorMax = new Vector2(1f, 0f);
        inputRt.pivot = new Vector2(1f, 0f);
        inputRt.sizeDelta = new Vector2(-120f, 56f);
        inputRt.anchoredPosition = new Vector2(0f, 72f);

        var inputBg = inputGo.AddComponent<Image>();
        inputBg.color = new Color32(255, 255, 255, 230);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(inputGo.transform, false);
        StretchFull(textRt);
        textRt.offsetMin = new Vector2(12f, 8f);
        textRt.offsetMax = new Vector2(-12f, -8f);

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.layer = LayerMask.NameToLayer("UI");
        var phRt = placeholderGo.GetComponent<RectTransform>();
        phRt.SetParent(inputGo.transform, false);
        StretchFull(phRt);
        phRt.offsetMin = new Vector2(14f, 8f);
        phRt.offsetMax = new Vector2(-12f, -8f);
        var phText = placeholderGo.AddComponent<Text>();
        phText.font = BuiltinUIFont;
        phText.fontSize = 24;
        phText.color = new Color32(120, 124, 132, 200);
        phText.text = "输入回答（测试用）";
        phText.supportRichText = false;

        var inputText = textGo.AddComponent<Text>();
        inputText.font = BuiltinUIFont;
        inputText.fontSize = 24;
        inputText.color = new Color32(40, 44, 52, 255);
        inputText.supportRichText = false;

        view.answerTextInput = inputGo.AddComponent<InputField>();
        view.answerTextInput.textComponent = inputText;
        view.answerTextInput.placeholder = phText;
        view.answerTextInput.lineType = InputField.LineType.SingleLine;

        view.answerTextSubmitButton = CreateFloatingButton(
            textPanelRt,
            "AnswerTextSubmit",
            "提交",
            new Vector2(108f, 56f),
            true);
        var submitRt = view.answerTextSubmitButton.GetComponent<RectTransform>();
        submitRt.anchorMin = new Vector2(1f, 0f);
        submitRt.anchorMax = new Vector2(1f, 0f);
        submitRt.pivot = new Vector2(1f, 0f);
        submitRt.anchoredPosition = new Vector2(0f, 72f);
    }

    public static void ConfigureAnswerVoiceIndicator(Button button)
    {
        if (button == null)
            return;
        button.interactable = false;
        var img = button.GetComponent<Image>();
        if (img != null)
            img.color = new Color32(52, 168, 83, 220);
        SetAnswerVoiceLabel(button, LeleVoiceAssistant.WakeHint);
    }

    public static void SetAnswerVoiceLabel(Button button, string label)
    {
        if (button == null)
            return;
        var text = button.GetComponentInChildren<Text>();
        if (text != null)
            text.text = label;
    }

    static void BuildCameraPreviewUi(StoryCreationPageView view, RectTransform root)
    {
        var miniRoot = new GameObject("CameraPreviewMini", typeof(RectTransform));
        miniRoot.layer = LayerMask.NameToLayer("UI");
        var miniRt = miniRoot.GetComponent<RectTransform>();
        miniRt.SetParent(root, false);
        miniRt.anchorMin = new Vector2(1f, 1f);
        miniRt.anchorMax = new Vector2(1f, 1f);
        miniRt.pivot = new Vector2(1f, 1f);
        miniRt.sizeDelta = new Vector2(CameraPreviewMiniWidth, CameraPreviewMiniHeight);
        miniRt.anchoredPosition = new Vector2(-CameraPreviewMargin, -CameraPreviewMargin);

        var miniFrame = CreateUiObject<Image>(miniRt, "Frame");
        StretchFull(miniFrame.rectTransform);
        miniFrame.color = new Color32(0, 0, 0, 140);

        view.cameraPreviewMini = CreateCameraPreviewRawImage(miniFrame.transform, "Preview");

        view.cameraPreviewMiniButton = miniFrame.gameObject.AddComponent<Button>();
        view.cameraPreviewMiniButton.targetGraphic = miniFrame;

        var hintGo = CreateUiObject<Image>(miniRt, "ExpandHint");
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(0f, 34f);
        hintRt.anchoredPosition = Vector2.zero;
        hintGo.color = new Color32(20, 24, 32, 150);
        hintGo.raycastTarget = false;

        var hintTextGo = new GameObject("Label", typeof(RectTransform));
        hintTextGo.layer = LayerMask.NameToLayer("UI");
        hintTextGo.transform.SetParent(hintGo.transform, false);
        StretchFull(hintTextGo.GetComponent<RectTransform>());
        var hintText = hintTextGo.AddComponent<Text>();
        hintText.font = BuiltinUIFont;
        hintText.fontSize = 18;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = Color.white;
        hintText.text = "点按放大";
        hintText.raycastTarget = false;

        view.cameraPreviewOverlay = new GameObject("CameraPreviewOverlay", typeof(RectTransform));
        view.cameraPreviewOverlay.layer = LayerMask.NameToLayer("UI");
        var overlayRt = view.cameraPreviewOverlay.GetComponent<RectTransform>();
        overlayRt.SetParent(root, false);
        StretchFull(overlayRt);
        view.cameraPreviewOverlay.SetActive(false);

        var backdrop = CreateUiObject<Image>(overlayRt, "Backdrop");
        StretchFull(backdrop.rectTransform);
        backdrop.color = new Color32(0, 0, 0, 170);
        view.cameraPreviewOverlayBackdropButton = backdrop.gameObject.AddComponent<Button>();
        view.cameraPreviewOverlayBackdropButton.targetGraphic = backdrop;

        var panel = CreateUiObject<Image>(overlayRt, "ExpandedPanel");
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1280f, 760f);
        panel.color = new Color32(255, 255, 255, 24);

        view.cameraPreviewExpanded = CreateCameraPreviewRawImage(panel.transform, "ExpandedPreview");
        view.cameraPreviewExpanded.color = Color.white;

        view.cameraPreviewExpandedPanelButton = panel.gameObject.AddComponent<Button>();
        view.cameraPreviewExpandedPanelButton.targetGraphic = panel;
    }

    static RawImage CreateCameraPreviewRawImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        StretchFull(rt);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        var raw = go.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        return raw;
    }

    static T CreateUiObject<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    static GameObject CreateUiLabel(Transform parent, string name, string text, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = BuiltinUIFont;
        t.fontSize = fontSize;
        t.color = new Color32(40, 44, 52, 255);
        t.text = text;
        t.alignment = align;
        return go;
    }

    static Text CreateOverlayText(Transform parent, string name, string text, int fontSize, TextAnchor align)
    {
        var go = CreateUiLabel(parent, name, text, fontSize, align);
        var t = go.GetComponent<Text>();
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 200);
        outline.effectDistance = new Vector2(2f, -2f);
        t.raycastTarget = false;
        return t;
    }

    static Button CreateFloatingButton(
        Transform parent,
        string name,
        string label,
        Vector2 size,
        bool primary)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = primary
            ? new Color32(255, 255, 255, 235)
            : new Color32(0, 0, 0, 120);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(go.transform, false);
        StretchFull(textRt);
        var text = textGo.AddComponent<Text>();
        text.font = BuiltinUIFont;
        text.fontSize = primary ? 30 : 26;
        text.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = primary ? new Color32(40, 44, 52, 255) : Color.white;
        text.text = label;

        if (!primary)
        {
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 160);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        go.GetComponent<RectTransform>().sizeDelta = size;
        return btn;
    }

    static void LayoutFloatingButton(Button button, float inset, bool alignRight, Vector2 size)
    {
        if (button == null)
            return;

        var rt = button.GetComponent<RectTransform>();
        var xAnchor = alignRight ? 1f : 0f;
        rt.anchorMin = new Vector2(xAnchor, 0f);
        rt.anchorMax = new Vector2(xAnchor, 0f);
        rt.pivot = new Vector2(xAnchor, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(alignRight ? -inset : inset, BottomInset);
    }

    static void LayoutChildButton(Button button, float x, float y, Vector2 size)
    {
        if (button == null)
            return;
        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
