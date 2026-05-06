using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时搭建教程页：TopBar / StepViewer（图+滑动）/ BottomBar（进度条+按钮）。
/// </summary>
[DefaultExecutionOrder(0)]
public class TutorialStepsPageBootstrap : MonoBehaviour
{
    public TutorialStepsConfig config;

    StepViewerUI _viewer;

    /// <summary>
    /// 与积木库 UI 一致：内置动态字体，可拾取系统字号中的汉字字形（TMP 默认 Liberation Sans 无 CJK）。
    /// </summary>
    static Font BuiltinUIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake()
    {
        if (config == null || config.steps == null || config.steps.Length == 0)
        {
            Debug.LogError("TutorialStepsPageBootstrap: 请在 Inspector 指定 TutorialStepsConfig，且 steps 非空。");
            return;
        }

        EnsureEventSystem();
        BuildUi();
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("TutorialCanvas", typeof(RectTransform));
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

        var bg = CreateUiObject<Image>(root, "Background");
        StretchFull(bg.rectTransform);
        bg.color = new Color32(248, 249, 252, 255);
        bg.raycastTarget = false;

        const float topH = 120f;
        const float bottomH = 150f;

        var topBar = CreateUiObject<Image>(root, "TopBar");
        var topRt = topBar.rectTransform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.sizeDelta = new Vector2(0f, topH);
        topRt.anchoredPosition = Vector2.zero;
        topBar.color = new Color32(255, 255, 255, 250);

        var backBtn = CreateTopBarBackButton(topRt, "BackButton", "← 返回");
        backBtn.onClick.AddListener(() =>
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.portfolioSceneName))
                SceneManager.LoadScene(config.portfolioSceneName.Trim());
        });

        var titleGo = CreateUiLabel(topRt, "Title", config.title, 36, TextAnchor.MiddleCenter);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.25f, 0f);
        titleRt.anchorMax = new Vector2(0.75f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        if (config.previewModelPrefab != null)
        {
            var previewBtn = CreateTopBarPreviewButton(topRt, "Preview3DButton", "3D 预览");
            var overlay = canvasGo.AddComponent<TutorialPreview3DOverlay>();
            overlay.Configure(config.previewModelPrefab, BuiltinUIFont);
            previewBtn.onClick.AddListener(overlay.Open);
        }

        var bottomBar = CreateUiObject<Image>(root, "BottomBar");
        var botRt = bottomBar.rectTransform;
        botRt.anchorMin = new Vector2(0f, 0f);
        botRt.anchorMax = new Vector2(1f, 0f);
        botRt.pivot = new Vector2(0.5f, 0f);
        botRt.sizeDelta = new Vector2(0f, bottomH);
        botRt.anchoredPosition = Vector2.zero;
        bottomBar.color = new Color32(255, 255, 255, 250);

        var bottomLayout = bottomBar.gameObject.AddComponent<VerticalLayoutGroup>();
        bottomLayout.padding = new RectOffset(32, 32, 16, 16);
        bottomLayout.spacing = 12f;
        bottomLayout.childAlignment = TextAnchor.UpperCenter;
        bottomLayout.childControlHeight = true;
        bottomLayout.childForceExpandHeight = false;

        var slider = CreateReadOnlyProgressSlider(bottomBar.rectTransform, "ProgressSlider");

        var stepLabelGo = CreateUiLabel(bottomBar.rectTransform, "StepLabel", "第 1 / 1 步", 28, TextAnchor.MiddleCenter);
        stepLabelGo.AddComponent<LayoutElement>().preferredHeight = 36f;

        var btnRow = new GameObject("ButtonRow", typeof(RectTransform));
        btnRow.layer = LayerMask.NameToLayer("UI");
        var rowRt = btnRow.GetComponent<RectTransform>();
        rowRt.SetParent(bottomBar.rectTransform, false);
        btnRow.AddComponent<LayoutElement>().preferredHeight = 72f;
        var h = btnRow.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;

        var prevBtn = CreateRowTextButton(rowRt, "PrevButton", "上一页", new Vector2(220f, 64f));
        var nextBtn = CreateRowTextButton(rowRt, "NextButton", "下一页", new Vector2(220f, 64f));

        var stepZone = CreateUiObject<Image>(root, "StepViewer");
        var zoneRt = stepZone.rectTransform;
        zoneRt.anchorMin = Vector2.zero;
        zoneRt.anchorMax = Vector2.one;
        zoneRt.offsetMin = new Vector2(24f, bottomH + 16f);
        zoneRt.offsetMax = new Vector2(-24f, -topH - 16f);
        stepZone.color = new Color32(255, 255, 255, 40);
        stepZone.raycastTarget = true;

        var fadeGroup = stepZone.gameObject.AddComponent<CanvasGroup>();

        var imgHolder = new GameObject("StepImageHolder", typeof(RectTransform));
        imgHolder.layer = LayerMask.NameToLayer("UI");
        var holderRt = imgHolder.GetComponent<RectTransform>();
        holderRt.SetParent(zoneRt, false);
        StretchFull(holderRt);
        holderRt.offsetMin = new Vector2(16f, 16f);
        holderRt.offsetMax = new Vector2(-16f, -16f);

        var stepImg = CreateUiObject<Image>(holderRt, "StepImage");
        StretchFull(stepImg.rectTransform);
        stepImg.preserveAspect = true;
        stepImg.color = Color.white;
        stepImg.raycastTarget = false;
        var sh = stepImg.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
        sh.effectDistance = new Vector2(4f, -4f);

        var logic = new GameObject("StepViewerLogic");
        logic.transform.SetParent(transform, false);
        _viewer = logic.AddComponent<StepViewerUI>();
        _viewer.stepImage = stepImg;
        _viewer.stepText = stepLabelGo.GetComponent<Text>();
        _viewer.nextButton = nextBtn;
        _viewer.prevButton = prevBtn;
        _viewer.progressBar = slider;
        _viewer.stepFadeGroup = fadeGroup;
        _viewer.steps = config.steps;

        prevBtn.onClick.AddListener(_viewer.PrevStep);
        nextBtn.onClick.AddListener(_viewer.NextStep);

        var swipe = stepZone.gameObject.AddComponent<SwipeStepNavigator>();
        swipe.viewer = _viewer;
    }

    static Slider CreateReadOnlyProgressSlider(Transform parent, string name)
    {
        var root = CreateUiChild(parent, name);
        var rootLe = root.AddComponent<LayoutElement>();
        rootLe.preferredHeight = 28f;
        rootLe.flexibleWidth = 1f;
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(0f, 28f);

        var bg = CreateUiChild(root.transform, "Background");
        StretchFull(bg.GetComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color32(226, 230, 238, 255);

        var fillArea = CreateUiChild(root.transform, "Fill Area");
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(4f, 4f);
        faRt.offsetMax = new Vector2(-4f, -4f);

        var fill = CreateUiChild(fillArea.transform, "Fill");
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color32(66, 135, 245, 255);

        var slider = root.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 0f;
        return slider;
    }

    static Button CreateTopBarPreviewButton(RectTransform topBar, string name, string label)
    {
        var go = CreateUiChild(topBar, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 72f);
        rt.anchoredPosition = new Vector2(-28f, 0f);

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = CreateUiChild(go.transform, "Text");
        StretchFull(textGo.GetComponent<RectTransform>());
        var t = textGo.AddComponent<Text>();
        t.font = BuiltinUIFont;
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color32(40, 44, 52, 255);
        t.text = label;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return btn;
    }

    static Button CreateTopBarBackButton(RectTransform topBar, string name, string label)
    {
        var go = CreateUiChild(topBar, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(200f, 72f);
        rt.anchoredPosition = new Vector2(28f, 0f);

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = CreateUiChild(go.transform, "Text");
        StretchFull(textGo.GetComponent<RectTransform>());
        var t = textGo.AddComponent<Text>();
        t.font = BuiltinUIFont;
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color32(40, 44, 52, 255);
        t.text = label;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return btn;
    }

    static Button CreateRowTextButton(Transform parent, string name, string label, Vector2 preferredSize)
    {
        var go = CreateUiChild(parent, name);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = CreateUiChild(go.transform, "Text");
        StretchFull(textGo.GetComponent<RectTransform>());
        var t = textGo.AddComponent<Text>();
        t.font = BuiltinUIFont;
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color32(40, 44, 52, 255);
        t.text = label;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return btn;
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
        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }

    static GameObject CreateUiLabel(Transform parent, string name, string content, float fontSize, TextAnchor align)
    {
        var go = CreateUiChild(parent, name);
        var t = go.AddComponent<Text>();
        t.font = BuiltinUIFont;
        t.fontSize = Mathf.RoundToInt(fontSize);
        t.alignment = align;
        t.color = new Color32(40, 44, 52, 255);
        t.text = content;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        StretchFull(go.GetComponent<RectTransform>());
        return go;
    }
}

/// <summary>
/// 步骤图 + 上下页按钮 + 进度条 + 文案；可配合 <see cref="SwipeStepNavigator"/> 滑动翻页。
/// </summary>
public class StepViewerUI : MonoBehaviour
{
    public Image stepImage;
    public Text stepText;
    public Button nextButton;
    public Button prevButton;
    public Slider progressBar;
    [Tooltip("换步时淡入；可空")]
    public CanvasGroup stepFadeGroup;

    public Sprite[] steps;

    int _current;

    void Start()
    {
        if (steps != null && steps.Length > 0)
            UpdateUI();
    }

    public void SetSteps(Sprite[] s)
    {
        steps = s;
        _current = 0;
        UpdateUI();
    }

    public void NextStep()
    {
        if (steps == null || steps.Length == 0)
            return;
        if (_current < steps.Length - 1)
        {
            _current++;
            UpdateUI();
        }
    }

    public void PrevStep()
    {
        if (steps == null || steps.Length == 0)
            return;
        if (_current > 0)
        {
            _current--;
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        if (steps == null || steps.Length == 0)
        {
            if (stepText != null)
                stepText.text = "无步骤图";
            return;
        }

        if (stepImage != null)
            stepImage.sprite = steps[_current];

        if (stepText != null)
            stepText.text = $"第 {_current + 1} / {steps.Length} 步";

        if (prevButton != null)
            prevButton.interactable = _current > 0;
        if (nextButton != null)
            nextButton.interactable = _current < steps.Length - 1;

        if (progressBar != null)
            progressBar.value = (float)(_current + 1) / steps.Length;

        if (stepFadeGroup != null)
        {
            StopAllCoroutines();
            StartCoroutine(FadeStepIn());
        }
    }

    IEnumerator FadeStepIn()
    {
        stepFadeGroup.alpha = 0f;
        while (stepFadeGroup.alpha < 1f)
        {
            stepFadeGroup.alpha += Time.unscaledDeltaTime * 5f;
            yield return null;
        }
        stepFadeGroup.alpha = 1f;
    }
}
