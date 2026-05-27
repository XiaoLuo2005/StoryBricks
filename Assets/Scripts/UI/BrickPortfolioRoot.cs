using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 故事库（StorySummary）或积木库（BrickLibrary）：按列表实例化 StoryCard Prefab。
/// </summary>
[DisallowMultipleComponent]
public class BrickPortfolioRoot : MonoBehaviour
{
    public enum PortfolioKind
    {
        /// <summary>使用 StoryCatalog / Resources 故事资产，选卡后先进入绘本再进搭建。</summary>
        StoryLibrary = 0,
        /// <summary>仅使用下方「作品列表」，选卡后直接进入教程场景。</summary>
        BrickWorks = 1,
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

    StoryCatalog _catalog;

    void Awake()
    {
        if (portfolioKind == PortfolioKind.StoryLibrary)
            _catalog = GetComponent<StoryCatalog>();
    }

    void Start()
    {
        if (headerTitleTextTmp != null && !string.IsNullOrEmpty(headerTitle))
            headerTitleTextTmp.text = headerTitle;

        if (cardListContent == null || cardPrefab == null)
        {
            Debug.LogError("BrickPortfolioRoot: 请绑定 Card List Content 与 Card Prefab（Assets/Prefabs/UI/StoryCard，含 StoryCardView）。");
            return;
        }

        var items = ResolveWorks();
        foreach (Transform child in cardListContent)
            Destroy(child.gameObject);

        foreach (var item in items)
        {
            if (item == null)
                continue;
            var card = Instantiate(cardPrefab, cardListContent);
            card.gameObject.SetActive(true);
            card.Bind(item, () => OnCardChosen(item));
        }

        ResizeScrollContent(items.Length);
    }

    BrickWorkItem[] ResolveWorks()
    {
        if (portfolioKind == PortfolioKind.BrickWorks)
            return works != null && works.Length > 0 ? works : Array.Empty<BrickWorkItem>();

        if (works != null && works.Length > 0)
            return works;

        if (_catalog != null)
        {
            var defs = _catalog.ResolveStories();
            if (defs != null && defs.Length > 0)
            {
                var list = new BrickWorkItem[defs.Length];
                for (int i = 0; i < defs.Length; i++)
                    list[i] = StoryCatalog.ToWorkItem(defs[i]);
                return list;
            }
        }

        var fromResources = Resources.LoadAll<StoryDefinition>("Stories");
        if (fromResources != null && fromResources.Length > 0)
        {
            var list = new BrickWorkItem[fromResources.Length];
            for (int i = 0; i < fromResources.Length; i++)
                list[i] = StoryCatalog.ToWorkItem(fromResources[i]);
            return list;
        }

        return Array.Empty<BrickWorkItem>();
    }

    void OnCardChosen(BrickWorkItem item)
    {
        if (string.IsNullOrWhiteSpace(item.tutorialSceneName))
        {
            Debug.LogWarning($"BrickPortfolio: 「{item.title}」未填写 tutorialSceneName。");
            return;
        }

        string buildScene = item.tutorialSceneName.Trim();

        if (portfolioKind == PortfolioKind.BrickWorks)
        {
            SceneManager.LoadScene(buildScene);
            return;
        }

        string prologueScene = string.IsNullOrWhiteSpace(item.prologueSceneName)
            ? defaultPrologueSceneName
            : item.prologueSceneName.Trim();

        string sid = string.IsNullOrWhiteSpace(item.storyId) ? item.title : item.storyId;
        StorySelectionContext.Set(
            sid,
            item.title,
            item.synopsisText ?? "",
            buildScene,
            item.thumbnail,
            item.prologuePages);
        SceneManager.LoadScene(prologueScene);
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
}
