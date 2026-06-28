using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 搭建教程页 uGUI 层级。Editor 生成 Prefab 后可在 Prefab 模式直接拖拽各区域微调位置。
/// </summary>
public static class TutorialStepsPageUiBuilder
{
    // 1920×1080 参考分辨率下的默认布局（可在 Prefab 里改 RectTransform）
    public const float TopBarHeight = 100f;
    public const float BottomControlsHeight = 140f;
    public const float LelePanelWidth = 380f;
    public const float LelePanelMarginRight = 20f;
    public const float ContentMarginLeft = 48f;
    public const float ContentMarginBottom = 24f;
    public const float ContentMarginTop = 20f;

    public static float ContentRightInset => LelePanelWidth + LelePanelMarginRight + 12f;

    public static Font BuiltinUIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static TutorialStepsPageView Build(Transform parent, string rootName = "TutorialCanvas")
    {
        var canvasGo = new GameObject(rootName, typeof(RectTransform));
        if (parent != null)
            canvasGo.transform.SetParent(parent, false);

        SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        StretchFull(root);

        var view = canvasGo.AddComponent<TutorialStepsPageView>();
        view.canvas = canvas;
        view.canvasScaler = scaler;

        var bg = CreateUiObject<Image>(root, "Background");
        StretchFull(bg.rectTransform);
        TutorialUiArt.ApplyBackground(bg);

        BuildTopBar(root, view);
        BuildStepViewer(root, view);
        BuildBottomControls(root, view);
        BuildAnchors(root, view);
        BuildStepViewerLogic(canvasGo.transform, view);

        return view;
    }

    static void BuildTopBar(RectTransform root, TutorialStepsPageView view)
    {
        var topBar = CreateUiObject<Image>(root, "TopBar");
        var topRt = topBar.rectTransform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.sizeDelta = new Vector2(0f, TopBarHeight);
        topRt.anchoredPosition = Vector2.zero;
        topBar.color = new Color(1f, 1f, 1f, 0f);
        topBar.raycastTarget = false;

        view.backButton = CreateTopBarBackButton(topRt, "BackButton", "← 返回");
        var backRt = view.backButton.GetComponent<RectTransform>();
        backRt.anchoredPosition = new Vector2(36f, -12f);

        view.titleText = CreateUiLabel(topRt, "Title", "教程", 40, TextAlignmentOptions.Center, FontStyles.Bold);
        var titleRt = view.titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0.18f, 0f);
        titleRt.anchorMax = new Vector2(0.62f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        view.preview3DButton = CreateTopBarPreviewButton(topRt, "Preview3DButton", "3D 预览");
        var previewRt = view.preview3DButton.GetComponent<RectTransform>();
        previewRt.anchoredPosition = new Vector2(-ContentRightInset + 24f, -12f);
        view.preview3DButton.gameObject.SetActive(false);
    }

    static void BuildStepViewer(RectTransform root, TutorialStepsPageView view)
    {
        var stepZone = CreateUiObject<Image>(root, "StepViewer");
        var zoneRt = stepZone.rectTransform;
        zoneRt.anchorMin = Vector2.zero;
        zoneRt.anchorMax = Vector2.one;
        zoneRt.offsetMin = new Vector2(ContentMarginLeft, BottomControlsHeight + ContentMarginBottom);
        zoneRt.offsetMax = new Vector2(-ContentRightInset, -TopBarHeight - ContentMarginTop);
        stepZone.color = new Color(1f, 1f, 1f, 0f);
        stepZone.raycastTarget = true;

        view.stepFadeGroup = stepZone.gameObject.AddComponent<CanvasGroup>();
        view.stepSwipeZone = zoneRt;

        var imgHolder = new GameObject("StepImageHolder", typeof(RectTransform));
        imgHolder.layer = LayerMask.NameToLayer("UI");
        var holderRt = imgHolder.GetComponent<RectTransform>();
        holderRt.SetParent(zoneRt, false);
        StretchFull(holderRt);
        holderRt.offsetMin = new Vector2(32f, 24f);
        holderRt.offsetMax = new Vector2(-32f, -24f);

        view.stepImage = CreateUiObject<Image>(holderRt, "StepImage");
        StretchFull(view.stepImage.rectTransform);
        view.stepImage.preserveAspect = true;
        view.stepImage.color = Color.white;
        view.stepImage.raycastTarget = false;
        var sh = view.stepImage.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.25f);
        sh.effectDistance = new Vector2(3f, -3f);

        view.stepLabelText = CreateUiLabel(
            zoneRt,
            "StepLabel",
            "第 1 / 1 步",
            32,
            TextAlignmentOptions.Center);
        var labelRt = view.stepLabelText.rectTransform;
        labelRt.anchorMin = new Vector2(0.08f, 0f);
        labelRt.anchorMax = new Vector2(0.92f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.sizeDelta = new Vector2(0f, 44f);
        labelRt.anchoredPosition = new Vector2(0f, 12f);
    }

    static void BuildBottomControls(RectTransform root, TutorialStepsPageView view)
    {
        var bottom = CreateUiObject<Image>(root, "BottomControls");
        var botRt = bottom.rectTransform;
        botRt.anchorMin = new Vector2(0.04f, 0f);
        botRt.anchorMax = new Vector2(0.72f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.sizeDelta = new Vector2(0f, BottomControlsHeight);
        botRt.anchoredPosition = new Vector2(0f, 18f);
        bottom.color = new Color(1f, 1f, 1f, 0f);
        bottom.raycastTarget = false;

        view.progressSlider = CreateReadOnlyProgressSlider(bottom.rectTransform, "ProgressSlider");
        var progressRt = view.progressSlider.GetComponent<RectTransform>();
        progressRt.anchorMin = new Vector2(0.06f, 0.72f);
        progressRt.anchorMax = new Vector2(0.94f, 0.72f);
        progressRt.pivot = new Vector2(0.5f, 0.5f);
        progressRt.sizeDelta = new Vector2(0f, 22f);
        progressRt.anchoredPosition = Vector2.zero;
        var prevRt = view.prevButton.GetComponent<RectTransform>();
        prevRt.anchorMin = new Vector2(0.5f, 0f);
        prevRt.anchorMax = new Vector2(0.5f, 0f);
        prevRt.pivot = new Vector2(1f, 0f);
        prevRt.anchoredPosition = new Vector2(-12f, 16f);

        view.nextButton = CreateRowTextButton(bottom.rectTransform, "NextButton", "下一页", new Vector2(200f, 60f));
        var nextRt = view.nextButton.GetComponent<RectTransform>();
        nextRt.anchorMin = new Vector2(0.5f, 0f);
        nextRt.anchorMax = new Vector2(0.5f, 0f);
        nextRt.pivot = new Vector2(0f, 0f);
        nextRt.anchoredPosition = new Vector2(12f, 16f);
    }

    /// <summary>Editor 修复旧 Prefab：删掉带 LayoutGroup 的旧底栏并重建（不含步数标签）。</summary>
    public static void ReplaceBottomControls(RectTransform root, TutorialStepsPageView view)
    {
        DestroyChildIfExists(root, "BottomBar");
        DestroyChildIfExists(root, "BottomControls");
        DestroyChildIfExists(root, "ButtonRow");
        BuildBottomControls(root, view);
    }

    /// <summary>Editor 修复旧 Prefab：把 StepLabel 放到 StepViewer 内并解锁 Pos。</summary>
    public static void EnsureStepLabel(RectTransform root, TutorialStepsPageView view)
    {
        var stepViewer = root.Find("StepViewer") as RectTransform;
        if (stepViewer == null)
            return;

        DestroyChildIfExists(stepViewer, "StepLabel");

        view.stepLabelText = CreateUiLabel(
            stepViewer,
            "StepLabel",
            "第 1 / 1 步",
            32,
            TextAlignmentOptions.Center);
        var labelRt = view.stepLabelText.rectTransform;
        labelRt.anchorMin = new Vector2(0.08f, 0f);
        labelRt.anchorMax = new Vector2(0.92f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.sizeDelta = new Vector2(0f, 44f);
        labelRt.anchoredPosition = new Vector2(0f, 12f);
    }

    static void DestroyChildIfExists(Transform root, string childName)
    {
        var t = root.Find(childName);
        if (t == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(t.gameObject);
        else
#endif
            Object.Destroy(t.gameObject);
    }

    static void BuildAnchors(RectTransform root, TutorialStepsPageView view)
    {
        var leleGo = new GameObject("LelePanelRoot", typeof(RectTransform));
        leleGo.layer = LayerMask.NameToLayer("UI");
        var leleRt = leleGo.GetComponent<RectTransform>();
        leleRt.SetParent(root, false);
        leleRt.anchorMin = new Vector2(1f, 0f);
        leleRt.anchorMax = new Vector2(1f, 1f);
        leleRt.pivot = new Vector2(1f, 0.5f);
        var vMargin = TopBarHeight + BottomControlsHeight + 48f;
        leleRt.sizeDelta = new Vector2(LelePanelWidth, -vMargin);
        leleRt.anchoredPosition = new Vector2(-LelePanelMarginRight, 8f);
        view.lelePanelRoot = leleRt;
        view.lelePanel = TutorialLelePanelUiBuilder.Build(leleRt);

        var mascotGo = new GameObject("MascotRoot", typeof(RectTransform));
        mascotGo.layer = LayerMask.NameToLayer("UI");
        var mascotRt = mascotGo.GetComponent<RectTransform>();
        mascotRt.SetParent(root, false);
        mascotRt.anchorMin = new Vector2(0f, 0f);
        mascotRt.anchorMax = new Vector2(0f, 0f);
        mascotRt.pivot = new Vector2(0f, 0f);
        mascotRt.sizeDelta = new Vector2(260f, 260f);
        mascotRt.anchoredPosition = new Vector2(32f, BottomControlsHeight + 12f);
        view.mascotRoot = mascotRt;
    }

    static void BuildStepViewerLogic(Transform logicParent, TutorialStepsPageView view)
    {
        var logic = new GameObject("StepViewerLogic");
        logic.transform.SetParent(logicParent, false);
        view.stepViewer = logic.AddComponent<StepViewerUI>();
        view.stepViewer.stepImage = view.stepImage;
        view.stepViewer.stepText = view.stepLabelText;
        view.stepViewer.nextButton = view.nextButton;
        view.stepViewer.prevButton = view.prevButton;
        view.stepViewer.progressBar = view.progressSlider;
        view.stepViewer.stepFadeGroup = view.stepFadeGroup;
    }

    public static Slider CreateReadOnlyProgressSlider(Transform parent, string name)
    {
        var root = CreateUiChild(parent, name);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 0.5f);
        rootRt.anchorMax = new Vector2(1f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(0f, 22f);
        rootRt.anchoredPosition = Vector2.zero;

        var bg = CreateUiChild(root.transform, "Background");
        StretchFull(bg.GetComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);

        var fillArea = CreateUiChild(root.transform, "Fill Area");
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(4f, 4f);
        faRt.offsetMax = new Vector2(-4f, -4f);

        var fill = CreateUiChild(fillArea.transform, "Fill");
        var fillRt = fill.GetComponent<RectTransform>();
        // 左锚点 + 左 pivot：填充只向右增长，不会整体偏移
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color32(255, 170, 60, 255);

        var slider = root.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        return slider;
    }

    public static Button CreateTopBarPreviewButton(RectTransform topBar, string name, string label)
    {
        var go = CreateUiChild(topBar, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(200f, 68f);

        var img = go.AddComponent<Image>();
        TutorialUiArt.ApplyButtonBackground(img);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        AddButtonLabel(go.transform, label, 28);
        return btn;
    }

    public static Button CreateTopBarBackButton(RectTransform topBar, string name, string label)
    {
        var go = CreateUiChild(topBar, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(190f, 68f);

        var img = go.AddComponent<Image>();
        TutorialUiArt.ApplyButtonBackground(img);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        AddButtonLabel(go.transform, label, 28);
        return btn;
    }

    public static Button CreateRowTextButton(Transform parent, string name, string label, Vector2 size)
    {
        var go = CreateUiChild(parent, name);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        TutorialUiArt.ApplyButtonBackground(img);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        AddButtonLabel(go.transform, label, 28);
        return btn;
    }

    static void AddButtonLabel(Transform buttonRoot, string label, float fontSize)
    {
        var textGo = CreateUiChild(buttonRoot, "Text");
        StretchFull(textGo.GetComponent<RectTransform>());
        var t = CreateUiLabel(textGo.transform, "Label", label, fontSize, TextAlignmentOptions.Center);
        StretchFull(t.rectTransform);
    }

    static GameObject CreateUiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        return go;
    }

    static T CreateUiObject<T>(Transform parent, string name) where T : Component
    {
        var go = CreateUiChild(parent, name);
        return go.AddComponent<T>();
    }

    public static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }

    static TextMeshProUGUI CreateUiLabel(
        Transform parent,
        string name,
        string content,
        float fontSize,
        TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var tmp = TutorialUiArt.CreateLabel(parent, name, content, fontSize, align, TutorialUiArt.TitleBrown, style);
        StretchFull(tmp.rectTransform);
        return tmp;
    }
}
