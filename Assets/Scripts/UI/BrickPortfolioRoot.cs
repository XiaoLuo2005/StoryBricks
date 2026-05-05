using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时搭建作品集界面：顶栏标题 + 下方三列（可配置）网格，纵向滚动；点击卡片加载对应教程场景。
/// </summary>
[DisallowMultipleComponent]
public class BrickPortfolioRoot : MonoBehaviour
{
    [Serializable]
    public class BrickWorkItem
    {
        public string title = "未命名作品";
        [Tooltip("须已加入 Build Settings 的场景名（不含路径）")]
        public string tutorialSceneName = "";
        [Tooltip("可选；留空则显示占位底色")]
        public Sprite thumbnail;
    }

    [Header("文案")]
    public string headerTitle = "积木库";

    [Header("作品列表")]
    public BrickWorkItem[] works;

    [Header("布局")]
    [Range(2, 6)]
    public int columns = 3;
    public Vector2 cellSize = new Vector2(320f, 380f);
    public Vector2 spacing = new Vector2(28f, 28f);
    public float headerHeight = 120f;
    public float pagePadding = 48f;

    [Header("配色")]
    public Color backgroundColor = new Color32(245, 247, 250, 255);
    public Color headerBarColor = new Color32(255, 255, 255, 255);
    public Color cardColor = new Color32(255, 255, 255, 255);
    public Color titleColor = new Color32(40, 44, 52, 255);
    public Color thumbPlaceholderColor = new Color32(230, 233, 239, 255);

    static Font BuiltinUIFont =>
        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake()
    {
        EnsureEventSystem();
        BuildUI(ResolveWorks());
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    BrickWorkItem[] ResolveWorks()
    {
        if (works != null && works.Length > 0)
            return works;
        return new[]
        {
            new BrickWorkItem { title = "StoryBricks · ArUco 生图", tutorialSceneName = "TestScene" },
            new BrickWorkItem { title = "教程模板（可复制改名）", tutorialSceneName = "TutorialTemplate" },
            new BrickWorkItem { title = "占位作品 C", tutorialSceneName = "TutorialTemplate" },
        };
    }

    void BuildUI(BrickWorkItem[] items)
    {
        var canvasGo = new GameObject("BrickPortfolioCanvas", typeof(RectTransform));
        SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var rootRt = canvasGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var bg = CreateUiObject<Image>(canvasGo.transform, "Background");
        StretchFull(bg.rectTransform);
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        var header = CreateUiObject<Image>(canvasGo.transform, "Header");
        var headerRt = header.rectTransform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(0f, headerHeight);
        headerRt.anchoredPosition = Vector2.zero;
        header.color = headerBarColor;
        header.raycastTarget = false;

        var title = CreateUiObject<Text>(headerRt, "Title");
        StretchFull(title.rectTransform);
        title.text = headerTitle;
        title.font = BuiltinUIFont;
        title.fontSize = 40;
        title.fontStyle = FontStyle.Bold;
        title.color = titleColor;
        title.alignment = TextAnchor.MiddleCenter;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Truncate;

        var scrollGo = CreateUiChild(canvasGo.transform, "ScrollView");
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(pagePadding, pagePadding);
        scrollRt.offsetMax = new Vector2(-pagePadding, -headerHeight - 24f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;

        var viewport = CreateUiObject<Image>(scrollRt, "Viewport");
        StretchFull(viewport.rectTransform);
        viewport.color = new Color(1f, 1f, 1f, 0.001f);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var contentGo = CreateUiChild(viewport.rectTransform, "Content");
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.padding = new RectOffset(12, 12, 12, 12);

        scroll.viewport = viewport.rectTransform;
        scroll.content = contentRt;

        foreach (var item in items)
        {
            if (item == null)
                continue;
            CreateCard(contentRt, item);
        }

        int n = items.Length;
        int rows = n <= 0 ? 1 : Mathf.CeilToInt(n / (float)columns);
        float gh = grid.padding.top + grid.padding.bottom + rows * cellSize.y + Mathf.Max(0, rows - 1) * spacing.y;
        contentRt.sizeDelta = new Vector2(0f, gh);
    }

    void CreateCard(RectTransform parent, BrickWorkItem item)
    {
        var cardGo = CreateUiChild(parent, "Card_" + SanitizeName(item.title));
        var img = cardGo.AddComponent<Image>();
        img.color = cardColor;
        var btn = cardGo.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.highlightedColor = new Color(0.94f, 0.96f, 1f);
        cb.pressedColor = new Color(0.86f, 0.90f, 1f);
        btn.colors = cb;

        string scene = item.tutorialSceneName;
        btn.onClick.AddListener(() =>
        {
            if (string.IsNullOrWhiteSpace(scene))
            {
                Debug.LogWarning($"BrickPortfolio: 「{item.title}」未填写教程场景名。");
                return;
            }
            SceneManager.LoadScene(scene.Trim());
        });

        var cardRt = cardGo.GetComponent<RectTransform>();

        var thumbGo = CreateUiChild(cardRt, "Thumbnail");
        var thumbRt = thumbGo.GetComponent<RectTransform>();
        thumbRt.anchorMin = new Vector2(0f, 0.22f);
        thumbRt.anchorMax = new Vector2(1f, 1f);
        thumbRt.offsetMin = new Vector2(16f, 16f);
        thumbRt.offsetMax = new Vector2(-16f, -8f);
        var thumbImg = thumbGo.AddComponent<Image>();
        thumbImg.preserveAspect = true;
        thumbImg.color = item.thumbnail != null ? Color.white : thumbPlaceholderColor;
        if (item.thumbnail != null)
            thumbImg.sprite = item.thumbnail;

        var titleGo = CreateUiChild(cardRt, "TitleBar");
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(1f, 0.22f);
        titleRt.offsetMin = new Vector2(12f, 10f);
        titleRt.offsetMax = new Vector2(-12f, -10f);
        var titleText = titleGo.AddComponent<Text>();
        titleText.text = item.title;
        titleText.font = BuiltinUIFont;
        titleText.fontSize = 24;
        titleText.color = titleColor;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    static string SanitizeName(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "Item";
        return s.Length <= 24 ? s.Replace("/", "_") : s.Substring(0, 24).Replace("/", "_");
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }
}
