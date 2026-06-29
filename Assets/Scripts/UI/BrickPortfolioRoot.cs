using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 故事库（StorySummary）、全量积木库（BrickLibrary）、故事作品集（StoryWorks）：按列表实例化 StoryCard Prefab。
/// </summary>
[DisallowMultipleComponent]
public class BrickPortfolioRoot : MonoBehaviour
{
    public enum PortfolioKind
    {
        /// <summary>使用 StoryCatalog / Resources 故事资产，选卡后先进入绘本再进故事作品集。</summary>
        StoryLibrary = 0,
        /// <summary>全量积木库：使用下方「作品列表」，选卡后直接进入教程场景。</summary>
        BrickWorks = 1,
        /// <summary>当前故事的作品子集：从 StorySelectionContext 读取，选卡后进入教程。</summary>
        StoryWorks = 2,
    }

    [Serializable]
    public class BrickWorkItem
    {
        public string storyId = "";
        public string title = "未命名作品";
        [TextArea(2, 6)]
        public string synopsisText = "";
        public Sprite[] prologuePages;
        public string prologueSceneName = "";
        public string tutorialSceneName = "";
        public Sprite thumbnail;
    }

    [Header("模式")]
    public PortfolioKind portfolioKind = PortfolioKind.StoryLibrary;

    [Header("UI（Prefab / 场景可视化编辑）")]
    public StoryLibraryPageView pageView;
    public StoryLibraryPageView pageViewPrefab;
    public bool allowRuntimeFallbackUi = true;
    [Tooltip("勾选后运行时自动创建/重排导航按钮；关闭则保留场景里可视化编辑的布局")]
    public bool applyRuntimeLayout = false;

    [Header("场景里摆好的 UI（未用 pageView 时）")]
    public TextMeshProUGUI headerTitleTextTmp;
    public RectTransform cardListContent;
    public StoryCardView cardPrefab;

    [Header("作品列表")]
    public BrickWorkItem[] works;

    [Header("场景跳转（故事库）")]
    public string defaultPrologueSceneName = StoryFlowScenes.StoryPrologue;
    public string headerTitle = "故事库";

    [Header("场景跳转（故事作品集）")]
    public string fallbackStoryLibrarySceneName = StoryFlowScenes.StoryLibrary;

    [Header("返回")]
    public bool showBackButton = true;
    public string storyLibraryBackSceneName = StoryFlowScenes.Start;
    public string storyWorksBackSceneName = StoryFlowScenes.StoryLibrary;
    public string brickWorksBackSceneName = StoryFlowScenes.Start;

    [Header("StoryWorks")]
    public bool showBrickLibraryButton = true;
    public string brickLibrarySceneName = StoryFlowScenes.BrickLibrary;
    public string brickLibraryButtonLabel = "积木库";
    [Tooltip("StoryWorks 模式下，学完积木教程后显示，进入分页故事创作")]
    public bool showStartCreationButton = true;
    public string startCreationButtonLabel = "开始创作故事";

    [Header("我的故事（StoryLibrary）")]
    public bool showMyStoriesButton = true;
    public string myStoriesButtonLabel = "我的故事";
    public string myStoriesSceneName = StoryFlowScenes.CompletedStoryLibrary;

    StoryCatalog _catalog;
    bool _pageViewBound;

    void Awake()
    {
        if (portfolioKind == PortfolioKind.StoryLibrary)
            _catalog = GetComponent<StoryCatalog>();

        BuildPageViewIfNeeded();
        TryCreateNavButtons();
        TryCreateStartCreationButton();
        TryCreateMyStoriesButton();
    }

    void Start()
    {
        if (portfolioKind == PortfolioKind.StoryWorks)
        {
            if (!StorySelectionContext.HasStoryWorks)
            {
                Debug.LogWarning("StoryWorks: 无故事上下文，返回故事库。");
                SceneManager.LoadScene(fallbackStoryLibrarySceneName.Trim());
                return;
            }

            if (headerTitleTextTmp != null)
            {
                StoryPageCaptionArt.EnsureChineseFont(headerTitleTextTmp);
                headerTitleTextTmp.text = StorySelectionContext.Title;
            }
        }
        else if (headerTitleTextTmp != null && !string.IsNullOrEmpty(headerTitle))
        {
            StoryPageCaptionArt.EnsureChineseFont(headerTitleTextTmp);
            headerTitleTextTmp.text = headerTitle;
        }

        if (cardListContent == null || cardPrefab == null)
        {
            Debug.LogError("BrickPortfolioRoot: 请绑定 Card List Content 与 Card Prefab（Assets/Prefabs/UI/StoryCard，含 StoryCardView）。");
            return;
        }

        if (portfolioKind == PortfolioKind.StoryLibrary)
            PopulateStoryLibrary();
        else
            PopulateWorkCards(ResolveWorks());
    }

    void BuildPageViewIfNeeded()
    {
        if (_pageViewBound)
            return;

        if (pageView != null && pageView.IsComplete)
        {
            ApplyPageViewBindings();
            return;
        }

        if (pageView != null)
        {
            pageView.WireFromSceneHierarchy(cardListContent);
            if (pageView.IsComplete)
            {
                ApplyPageViewBindings();
                return;
            }
        }

        if (TryAdoptScenePageView())
        {
            ApplyPageViewBindings();
            return;
        }

        if (pageViewPrefab != null)
        {
            pageView = Instantiate(pageViewPrefab);
            pageView.name = pageViewPrefab.name;
            ApplyPageViewBindings();
            return;
        }

        if (!allowRuntimeFallbackUi)
            return;

        var resourcesPrefab = Resources.Load<StoryLibraryPageView>(GetResourcesPagePrefabPath());
        if (resourcesPrefab != null && cardListContent == null)
        {
            pageView = Instantiate(resourcesPrefab);
            pageView.name = resourcesPrefab.name;
            ApplyPageViewBindings();
            return;
        }

        if (cardListContent != null)
            return;

        if (portfolioKind == PortfolioKind.BrickWorks)
        {
            pageView = StoryLibraryUiBuilder.BuildBrickLibraryPageView(null);
            ApplyPageViewBindings();
        }
    }

    bool TryAdoptScenePageView()
    {
        if (cardListContent == null && pageView == null)
            return false;

        if (pageView == null && cardListContent != null)
        {
            var canvas = cardListContent.GetComponentInParent<Canvas>();
            if (canvas != null)
                pageView = canvas.GetComponent<StoryLibraryPageView>();
        }

        if (pageView == null)
            return false;

        pageView.WireFromSceneHierarchy(cardListContent);
        return pageView.IsComplete;
    }

    string GetResourcesPagePrefabPath()
    {
        return portfolioKind == PortfolioKind.BrickWorks
            ? "UI/BrickLibraryPage"
            : "UI/CompletedStoryLibraryPage";
    }

    void ApplyPageViewBindings()
    {
        if (pageView == null || !pageView.IsComplete)
            return;

        cardListContent = pageView.cardListContent;
        headerTitleTextTmp = pageView.headerTitle;
        _pageViewBound = true;
    }

    void PopulateStoryLibrary()
    {
        var defs = ResolveStoryDefinitions();
        foreach (Transform child in cardListContent)
            Destroy(child.gameObject);

        int count = 0;
        foreach (var def in defs)
        {
            if (def == null)
                continue;
            var item = StoryCatalog.ToWorkItem(def);
            if (item == null)
                continue;
            var card = Instantiate(cardPrefab, cardListContent);
            card.gameObject.SetActive(true);
            card.Bind(item, () => OnStoryChosen(def));
            count++;
        }

        StoryLibraryUiBuilder.ResizeScrollContent(cardListContent, count);
    }

    void PopulateWorkCards(BrickWorkItem[] items)
    {
        foreach (Transform child in cardListContent)
            Destroy(child.gameObject);

        int count = 0;
        foreach (var item in items)
        {
            if (item == null)
                continue;
            var card = Instantiate(cardPrefab, cardListContent);
            card.gameObject.SetActive(true);
            card.Bind(item, () => OnWorkChosen(item));
            count++;
        }

        StoryLibraryUiBuilder.ResizeScrollContent(cardListContent, count);
    }

    StoryDefinition[] ResolveStoryDefinitions()
    {
        if (_catalog != null)
        {
            var defs = _catalog.ResolveStories();
            if (defs != null && defs.Length > 0)
                return defs;
        }

        return Resources.LoadAll<StoryDefinition>("Stories");
    }

    BrickWorkItem[] ResolveWorks()
    {
        if (portfolioKind == PortfolioKind.StoryWorks)
            return StorySelectionContext.Works ?? Array.Empty<BrickWorkItem>();

        if (portfolioKind == PortfolioKind.BrickWorks)
            return works != null && works.Length > 0 ? works : Array.Empty<BrickWorkItem>();

        return Array.Empty<BrickWorkItem>();
    }

    void OnStoryChosen(StoryDefinition def)
    {
        if (def.works == null || def.works.Length == 0)
        {
            Debug.LogWarning(
                $"BrickPortfolio: 故事「{def.title}」未配置 Works（关联积木作品），无法进入绘本。请在 StoryDefinition 里添加至少一项作品并填写 Tutorial Scene Name。");
            return;
        }

        StorySelectionContext.SetFromStory(def);
        if (!StorySelectionContext.HasStoryWorks)
        {
            Debug.LogWarning(
                $"BrickPortfolio: 故事「{def.title}」的 Works 里没有有效的 Tutorial Scene Name，无法继续。");
            return;
        }

        string prologueScene = string.IsNullOrWhiteSpace(def.prologueSceneName)
            ? defaultPrologueSceneName
            : def.prologueSceneName.Trim();
        SceneManager.LoadScene(prologueScene);
    }

    void OnWorkChosen(BrickWorkItem item)
    {
        if (string.IsNullOrWhiteSpace(item.tutorialSceneName))
        {
            Debug.LogWarning($"BrickPortfolio: 「{item.title}」未填写 tutorialSceneName。");
            return;
        }

        SceneManager.LoadScene(item.tutorialSceneName.Trim());
    }

    void TryCreateNavButtons()
    {
        var canvas = cardListContent != null
            ? cardListContent.GetComponentInParent<Canvas>()
            : null;
        canvas ??= pageView?.canvas;
        canvas ??= FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        if (!applyRuntimeLayout)
        {
            WireExistingNavButtons(canvas);
            return;
        }

        int column = 0;

        if (showBackButton)
        {
            string sceneName = portfolioKind switch
            {
                PortfolioKind.StoryLibrary => storyLibraryBackSceneName,
                PortfolioKind.StoryWorks => storyWorksBackSceneName,
                PortfolioKind.BrickWorks => StorySelectionContext.ResolvePortfolioReturnScene(brickWorksBackSceneName),
                _ => "",
            };

            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                string label = portfolioKind switch
                {
                    PortfolioKind.StoryLibrary => "← 返回封面",
                    PortfolioKind.StoryWorks => "← 返回故事库",
                    PortfolioKind.BrickWorks => StorySelectionContext.HasStoryWorks
                        ? "← 返回作品集"
                        : "← 返回封面",
                    _ => "← 返回",
                };

                if (pageView?.backButton != null)
                {
                    StoryFlowBackButtonUi.BindNavigation(pageView.backButton, label, sceneName);
                    pageView.backButton.transform.SetAsLastSibling();
                }
                else
                {
                    StoryFlowBackButtonUi.EnsureTopLeft(canvas, label, sceneName);
                }

                column++;
            }
        }

        if (portfolioKind == PortfolioKind.StoryWorks &&
            showBrickLibraryButton &&
            !string.IsNullOrWhiteSpace(brickLibrarySceneName))
        {
            StoryFlowBackButtonUi.EnsureTopLeft(
                canvas,
                "BrickLibraryButton",
                brickLibraryButtonLabel,
                brickLibrarySceneName,
                column);
        }
    }

    void WireExistingNavButtons(Canvas canvas)
    {
        if (showBackButton)
        {
            string sceneName = portfolioKind switch
            {
                PortfolioKind.StoryLibrary => storyLibraryBackSceneName,
                PortfolioKind.StoryWorks => storyWorksBackSceneName,
                PortfolioKind.BrickWorks => StorySelectionContext.ResolvePortfolioReturnScene(brickWorksBackSceneName),
                _ => "",
            };

            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                string label = portfolioKind switch
                {
                    PortfolioKind.StoryLibrary => "← 返回封面",
                    PortfolioKind.StoryWorks => "← 返回故事库",
                    PortfolioKind.BrickWorks => StorySelectionContext.HasStoryWorks
                        ? "← 返回作品集"
                        : "← 返回封面",
                    _ => "← 返回",
                };

                var back = pageView?.backButton;
                if (back == null)
                {
                    var backTf = canvas.transform.Find("BackButton");
                    if (backTf != null)
                        back = backTf.GetComponent<Button>();
                }

                if (back != null)
                {
                    StoryFlowBackButtonUi.BindNavigation(back, label, sceneName);
                    back.transform.SetAsLastSibling();
                }
            }
        }

        if (portfolioKind != PortfolioKind.StoryWorks ||
            !showBrickLibraryButton ||
            string.IsNullOrWhiteSpace(brickLibrarySceneName))
            return;

        var brickTf = canvas.transform.Find("BrickLibraryButton");
        if (brickTf == null)
            return;

        var brickBtn = brickTf.GetComponent<Button>();
        if (brickBtn != null)
            StoryFlowBackButtonUi.BindNavigation(brickBtn, brickLibraryButtonLabel, brickLibrarySceneName);
    }

    void TryCreateStartCreationButton()
    {
        if (portfolioKind != PortfolioKind.StoryWorks ||
            !showStartCreationButton ||
            !StorySelectionContext.HasCreationPages)
            return;

        var canvas = cardListContent != null
            ? cardListContent.GetComponentInParent<Canvas>()
            : null;
        canvas ??= pageView?.canvas;
        canvas ??= FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        var canvasRt = canvas.GetComponent<RectTransform>();
        var existing = canvasRt.Find("StartCreationButton");
        if (existing == null)
        {
            if (!applyRuntimeLayout)
                return;
        }
        else if (!applyRuntimeLayout)
        {
            var existingBtn = existing.GetComponent<Button>();
            if (existingBtn != null)
                WireStartCreationButton(existingBtn);
            return;
        }

        Button btn;
        if (existing != null)
        {
            btn = existing.GetComponent<Button>();
            if (btn == null)
                return;
        }
        else
        {
            var go = new GameObject("StartCreationButton", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRt, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(280f, 88f);
            rt.anchoredPosition = new Vector2(-40f, 40f);

            var img = go.AddComponent<Image>();
            img.color = new Color32(52, 168, 83, 255);
            btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.layer = LayerMask.NameToLayer("UI");
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = startCreationButtonLabel;
        }

        WireStartCreationButton(btn);
    }

    void WireStartCreationButton(Button btn)
    {
        if (btn == null)
            return;

        var labelText = btn.GetComponentInChildren<Text>();
        if (labelText != null)
            labelText.text = startCreationButtonLabel;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnStartCreationClicked);
        btn.transform.SetAsLastSibling();
    }

    void TryCreateMyStoriesButton()
    {
        if (portfolioKind != PortfolioKind.StoryLibrary ||
            !showMyStoriesButton ||
            string.IsNullOrWhiteSpace(myStoriesSceneName))
            return;

        var canvas = cardListContent != null
            ? cardListContent.GetComponentInParent<Canvas>()
            : null;
        canvas ??= pageView?.canvas;
        canvas ??= FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        var canvasRt = canvas.GetComponent<RectTransform>();
        var existing = canvasRt.Find("MyStoriesButton");
        if (existing == null)
        {
            if (!applyRuntimeLayout)
                return;
        }
        else if (!applyRuntimeLayout)
        {
            var existingBtn = existing.GetComponent<Button>();
            if (existingBtn != null)
                WireMyStoriesButton(existingBtn);
            return;
        }

        Button btn;
        if (existing != null)
        {
            btn = existing.GetComponent<Button>();
            if (btn == null)
                return;
        }
        else
        {
            var go = new GameObject("MyStoriesButton", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRt, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(280f, 88f);
            rt.anchoredPosition = new Vector2(-40f, 40f);

            var img = go.AddComponent<Image>();
            img.color = new Color32(142, 68, 173, 255);
            btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.layer = LayerMask.NameToLayer("UI");
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = myStoriesButtonLabel;
        }

        WireMyStoriesButton(btn);
    }

    void WireMyStoriesButton(Button btn)
    {
        if (btn == null)
            return;

        var labelText = btn.GetComponentInChildren<Text>();
        if (labelText != null)
            labelText.text = myStoriesButtonLabel;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => SceneManager.LoadScene(myStoriesSceneName.Trim()));
        btn.transform.SetAsLastSibling();
    }

    void OnStartCreationClicked()
    {
        if (!StorySelectionContext.HasCreationPages)
        {
            Debug.LogWarning("StoryWorks: 当前故事未配置 creationPages，无法进入故事创作。");
            return;
        }

        StorySessionCache.BeginSession(StorySelectionContext.StoryId, StorySelectionContext.Title);
        var sceneName = StorySelectionContext.ResolveCreationSceneName();
        Debug.Log($"StoryWorks: 进入分页故事创作 → {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
