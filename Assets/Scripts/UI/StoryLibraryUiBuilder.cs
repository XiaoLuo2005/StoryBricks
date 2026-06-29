using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>与 StorySummary 故事库一致的背景装饰、Canvas 与滚动列表搭建。</summary>
public static class StoryLibraryUiBuilder
{
    const string BackgroundResourcePath = "StorySummary/Background";
    const string TitleBannerResourcePath = "StorySummary/TitleBanner";
    const string BrickLibraryTitleBannerResourcePath = "BrickLibrary/TitleBanner";
    const string BrickLibraryHeaderSpritePath = "Assets/Art/积木库.png";
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
        var pageView = BuildPageView(null, options);
        if (pageView == null)
            return null;

        return new BuiltUi
        {
            Canvas = pageView.canvas,
            HeaderTitle = pageView.headerTitle,
            ScrollRect = pageView.scrollRect,
            CardListContent = pageView.cardListContent,
            EmptyHint = pageView.emptyHint,
        };
    }

    /// <summary>生成带 StoryLibraryPageView 的 Canvas，供 Prefab / 场景可视化编辑。</summary>
    public static StoryLibraryPageView BuildPageView(Transform parent, BuildOptions options)
    {
        options ??= new BuildOptions();
        if (Application.isPlaying)
        {
            EnsureEventSystem();
            EnsureMainCamera();
        }

        var decorRoot = EnsureSceneDecorRoot(options.useStoryLibraryTitleBanner);

        var canvasGo = new GameObject("CompletedStoryLibraryCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Stretch(canvasGo.GetComponent<RectTransform>());

        var view = canvasGo.AddComponent<StoryLibraryPageView>();
        view.canvas = canvas;
        view.decorRoot = decorRoot;

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

        view.headerTitle = headerTmp;

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
        view.scrollRect = scrollRect;
        view.cardListContent = contentRt;

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

        view.emptyHint = emptyHintGo;
        view.backButton = CreateBackButton(canvas.transform);

        return view;
    }

    /// <summary>积木库页：图片顶栏 + 滚动列表 + 返回按钮，供 Prefab / 场景可视化编辑。</summary>
    public static StoryLibraryPageView BuildBrickLibraryPageView(Transform parent)
    {
        if (Application.isPlaying)
        {
            EnsureEventSystem();
            EnsureMainCamera();
        }

        var decorRoot = EnsureBrickLibraryDecor();

        var canvasGo = new GameObject("BrickLibraryCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Stretch(canvasGo.GetComponent<RectTransform>());

        var view = canvasGo.AddComponent<StoryLibraryPageView>();
        view.canvas = canvas;
        view.decorRoot = decorRoot;

        var header = Child(canvas.transform, "HeaderTitle");
        var headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(520f, 140f);
        headerRt.anchoredPosition = new Vector2(0f, -52f);
        var headerImg = header.AddComponent<Image>();
        headerImg.preserveAspect = true;
        headerImg.raycastTarget = false;
#if UNITY_EDITOR
        var headerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BrickLibraryHeaderSpritePath);
        if (headerSprite != null)
            headerImg.sprite = headerSprite;
#endif
        view.headerTitleImage = headerImg;

        var scroll = Child(canvas.transform, "ScrollView");
        var scrollRt = scroll.GetComponent<RectTransform>();
        Stretch(scrollRt);
        scrollRt.offsetMax = new Vector2(0f, -110f);
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
        contentRt.sizeDelta = new Vector2(0f, 800f);
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(320f, 380f);
        grid.spacing = new Vector2(28f, 28f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRt;
        view.scrollRect = scrollRect;
        view.cardListContent = contentRt;
        view.emptyHint = null;
        view.backButton = CreateBackButton(canvas.transform);

        return view;
    }

    public static Transform EnsureBrickLibraryDecorVisible()
    {
        return EnsureBrickLibraryDecor();
    }

    static Transform EnsureBrickLibraryDecor()
    {
        var existing = GameObject.Find("StoryLibraryDecor");
        if (existing != null)
            return existing.transform;

        var root = new GameObject("StoryLibraryDecor");

        var background = Resources.Load<Sprite>(BackgroundResourcePath);
        if (background != null)
            SpawnSprite(root.transform, "Background", background, BackgroundPosition, Vector3.one, 0);

        var titleBanner = Resources.Load<Sprite>(BrickLibraryTitleBannerResourcePath);
        if (titleBanner != null)
            SpawnSprite(
                root.transform,
                "TitleBanner",
                titleBanner,
                TitleBannerPosition,
                Vector3.one * TitleBannerScale,
                1);

        return root.transform;
    }

    public static Button CreateBackButton(Transform canvasTransform)
    {
        var go = Child(canvasTransform, "BackButton");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -28f);
        rt.sizeDelta = new Vector2(200f, 72f);

        var img = go.AddComponent<Image>();
        TutorialUiArt.ApplyButtonBackground(img);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelGo = Child(go.transform, "Label");
        Stretch(labelGo.GetComponent<RectTransform>());
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        var font = LoadTitleFont();
        if (font != null)
            tmp.font = font;
        tmp.text = "← 返回";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = TutorialUiArt.TitleBrown;

        return btn;
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

    /// <summary>在场景里创建 StoryLibraryDecor（若尚未存在），可在 Scene 视图拖拽背景。</summary>
    public static Transform EnsureSceneDecorVisible(bool useStoryLibraryTitleBanner = false)
    {
        return EnsureSceneDecorRoot(useStoryLibraryTitleBanner);
    }

    static void EnsureSceneDecor(bool useStoryLibraryTitleBanner)
    {
        EnsureSceneDecorRoot(useStoryLibraryTitleBanner);
    }

    static Transform EnsureSceneDecorRoot(bool useStoryLibraryTitleBanner)
    {
        var legacy = GameObject.Find("StoryLibraryBackground");
        if (legacy != null)
            Object.Destroy(legacy);

        var existing = GameObject.Find("StoryLibraryDecor");
        if (existing != null)
            return existing.transform;

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

        return root.transform;
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
