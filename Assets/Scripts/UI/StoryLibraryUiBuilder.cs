using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>与 StorySummary 故事库一致的背景装饰、Canvas 与滚动列表搭建。</summary>
public static class StoryLibraryUiBuilder
{
    const string BackgroundResourcePath = "StorySummary/Background";
    const string TitleBannerResourcePath = "StorySummary/TitleBanner";
    const string CardPrefabResourcePath = "UI/StoryCard";
    const string TitleFontResourcePath = "UI/word SDF";

    static readonly Color TitleBrown = new Color(0.35f, 0.18f, 0.08f, 1f);
    static readonly Vector3 BackgroundPosition = new Vector3(0f, -0.4f, 0.5f);
    static readonly Vector3 TitleBannerPosition = new Vector3(0f, 5.1f, -1.2f);
    const float TitleBannerScale = 0.15737f;

    public sealed class BuildOptions
    {
        public string headerTitle = "故事库";
        public string emptyHint = "";
        /// <summary>为 true 时使用「故事库」顶栏美术字；为 false 时用 TMP 显示自定义标题（如「我的故事」）。</summary>
        public bool useStoryLibraryTitleBanner;
    }

    public sealed class BuiltUi
    {
        public Canvas Canvas;
        public TextMeshProUGUI HeaderTitle;
        public ScrollRect ScrollRect;
        public RectTransform CardListContent;
        public GameObject EmptyHint;
    }

    public static BuiltUi Build(string headerTitle, string emptyHint) =>
        Build(new BuildOptions
        {
            headerTitle = headerTitle,
            emptyHint = emptyHint,
            useStoryLibraryTitleBanner = headerTitle == "故事库",
        });

    public static BuiltUi Build(BuildOptions options)
    {
        options ??= new BuildOptions();
        EnsureEventSystem();
        EnsureMainCamera();
        EnsureSceneDecor(options.useStoryLibraryTitleBanner);

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Stretch(canvasGo.GetComponent<RectTransform>());

        TextMeshProUGUI headerTmp = null;
        if (!options.useStoryLibraryTitleBanner)
        {
            var titleFont = LoadTitleFont();
            var header = Child(canvas.transform, "HeaderTitle");
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.12f, 1f);
            headerRt.anchorMax = new Vector2(0.88f, 1f);
            headerRt.offsetMin = new Vector2(0f, -132f);
            headerRt.offsetMax = new Vector2(0f, -24f);
            headerTmp = header.AddComponent<TextMeshProUGUI>();
            if (titleFont != null)
                headerTmp.font = titleFont;
            headerTmp.text = options.headerTitle;
            headerTmp.fontSize = 52;
            headerTmp.color = TitleBrown;
            headerTmp.alignment = TextAlignmentOptions.Center;
            headerTmp.fontStyle = FontStyles.Bold;
            headerTmp.outlineWidth = 0.22f;
            headerTmp.outlineColor = Color.white;
        }

        var scroll = Child(canvas.transform, "ScrollView");
        var scrollRt = scroll.GetComponent<RectTransform>();
        Stretch(scrollRt);
        scrollRt.offsetMax = new Vector2(0f, -110f);
        scrollRt.offsetMin = new Vector2(0f, 72f);
        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewport = Child(scroll.transform, "Viewport");
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var content = Child(viewport.transform, "Content");
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 800f);
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(320f, 380f);
        grid.spacing = new Vector2(28f, 28f);
        grid.padding = new RectOffset(48, 48, 36, 48);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRt;

        GameObject emptyHintGo = null;
        if (!string.IsNullOrWhiteSpace(options.emptyHint))
        {
            emptyHintGo = Child(canvas.transform, "EmptyHint");
            var emptyRt = emptyHintGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = new Vector2(0.5f, 0.5f);
            emptyRt.anchorMax = new Vector2(0.5f, 0.5f);
            emptyRt.pivot = new Vector2(0.5f, 0.5f);
            emptyRt.sizeDelta = new Vector2(860f, 220f);
            emptyRt.anchoredPosition = new Vector2(0f, 20f);
            var emptyTmp = emptyHintGo.AddComponent<TextMeshProUGUI>();
            var titleFont = LoadTitleFont();
            if (titleFont != null)
                emptyTmp.font = titleFont;
            emptyTmp.text = options.emptyHint;
            emptyTmp.fontSize = 30;
            emptyTmp.color = TitleBrown;
            emptyTmp.alignment = TextAlignmentOptions.Center;
            emptyTmp.enableWordWrapping = true;
            emptyHintGo.SetActive(false);
        }

        return new BuiltUi
        {
            Canvas = canvas,
            HeaderTitle = headerTmp,
            ScrollRect = scrollRect,
            CardListContent = contentRt,
            EmptyHint = emptyHintGo,
        };
    }

    public static StoryCardView LoadCardPrefab()
    {
        var prefab = Resources.Load<GameObject>(CardPrefabResourcePath);
        return prefab != null ? prefab.GetComponent<StoryCardView>() : null;
    }

    public static void ResizeScrollContent(RectTransform content, int itemCount)
    {
        if (content == null)
            return;

        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        int columns = Mathf.Max(1, grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? grid.constraintCount
            : 3);
        int rows = itemCount <= 0 ? 1 : Mathf.CeilToInt(itemCount / (float)columns);
        float h = grid.padding.top + grid.padding.bottom +
                  rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(h, 420f));
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static void EnsureMainCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.transform.position = new Vector3(0f, 1f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
    }

    static void EnsureSceneDecor(bool useStoryLibraryTitleBanner)
    {
        var legacy = GameObject.Find("StoryLibraryBackground");
        if (legacy != null)
            Object.Destroy(legacy);

        if (GameObject.Find("StoryLibraryDecor") != null)
            return;

        var root = new GameObject("StoryLibraryDecor");

        var background = Resources.Load<Sprite>(BackgroundResourcePath);
        if (background != null)
        {
            SpawnSprite(root.transform, "Background", background, BackgroundPosition, Vector3.one, 0);
        }
        else
        {
            Debug.LogWarning("[StoryLibraryUi] 未找到背景 Sprite：Resources/StorySummary/Background");
        }

        if (useStoryLibraryTitleBanner)
        {
            var titleBanner = Resources.Load<Sprite>(TitleBannerResourcePath);
            if (titleBanner != null)
            {
                SpawnSprite(
                    root.transform,
                    "TitleBanner",
                    titleBanner,
                    TitleBannerPosition,
                    Vector3.one * TitleBannerScale,
                    1);
            }
        }
    }

    static void SpawnSprite(
        Transform parent,
        string name,
        Sprite sprite,
        Vector3 worldPosition,
        Vector3 localScale,
        int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.localScale = localScale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
    }

    static TMP_FontAsset LoadTitleFont()
    {
        return Resources.Load<TMP_FontAsset>(TitleFontResourcePath);
    }

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
