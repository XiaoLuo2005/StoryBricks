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

    [Header("场景里摆好的 UI")]
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

    StoryCatalog _catalog;

    void Awake()
    {
        if (portfolioKind == PortfolioKind.StoryLibrary)
            _catalog = GetComponent<StoryCatalog>();

        TryCreateNavButtons();
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
                headerTitleTextTmp.text = StorySelectionContext.Title;
        }
        else if (headerTitleTextTmp != null && !string.IsNullOrEmpty(headerTitle))
        {
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

        ResizeScrollContent(count);
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

        ResizeScrollContent(count);
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

    void ResizeScrollContent(int itemCount)
    {
        var grid = cardListContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;
        int columns = Mathf.Max(1, grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount ? grid.constraintCount : 3);
        int rows = itemCount <= 0 ? 1 : Mathf.CeilToInt(itemCount / (float)columns);
        float h = grid.padding.top + grid.padding.bottom + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        cardListContent.sizeDelta = new Vector2(cardListContent.sizeDelta.x, h);
    }

    void TryCreateNavButtons()
    {
        var canvas = cardListContent != null
            ? cardListContent.GetComponentInParent<Canvas>()
            : null;
        canvas ??= FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

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
                StoryFlowBackButtonUi.EnsureTopLeft(canvas, label, sceneName);
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
}
