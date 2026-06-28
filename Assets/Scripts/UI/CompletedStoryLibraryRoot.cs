using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>「我的故事」绘本合集列表：UI 样式与 StorySummary 故事库一致。</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class CompletedStoryLibraryRoot : MonoBehaviour
{
    public string headerTitle = "我的故事";
    public string backSceneName = StoryFlowScenes.StoryLibrary;
    public string viewerSceneName = StoryFlowScenes.CompletedStoryViewer;
    public string emptyHint = "还没有完成的故事绘本。\n完成一次故事创作后会自动保存在这里。";
    public string cardButtonLabel = "阅读";
    public StoryCardView cardPrefab;

    [Header("UI（Prefab / 场景可视化编辑）")]
    public StoryLibraryPageView pageView;
    public StoryLibraryPageView pageViewPrefab;
    public bool allowRuntimeFallbackUi = true;

    StoryLibraryUiBuilder.BuiltUi _ui;
    bool _uiBuilt;

    void Awake()
    {
        BuildUiIfNeeded();
    }

    void Start()
    {
        BuildUiIfNeeded();
        RefreshCards();
    }

    void OnEnable()
    {
        if (_uiBuilt)
            RefreshCards();
    }

    void BuildUiIfNeeded()
    {
        if (_uiBuilt)
            return;

        EnsurePageView();
        if (pageView == null || !pageView.IsComplete)
        {
            Debug.LogError("[CompletedStory] 未找到可用的 StoryLibraryPageView。");
            return;
        }

        BindPageView();
        _uiBuilt = true;
    }

    void EnsurePageView()
    {
        if (pageView != null && pageView.IsComplete)
            return;

        if (pageViewPrefab == null)
            pageViewPrefab = Resources.Load<StoryLibraryPageView>("UI/CompletedStoryLibraryPage");

        if (pageViewPrefab != null)
        {
            pageView = Instantiate(pageViewPrefab);
            pageView.name = pageViewPrefab.name;
            return;
        }

        if (!allowRuntimeFallbackUi)
            return;

        Debug.LogWarning(
            "[CompletedStory] 未配置 pageView，正在运行时临时搭建 UI。" +
            "请运行菜单 StoryBricks/我的故事/挂载可视化 UI。");
        pageView = StoryLibraryUiBuilder.BuildPageView(null, CreateBuildOptions());
    }

    void BindPageView()
    {
        _ui = new StoryLibraryUiBuilder.BuiltUi
        {
            Canvas = pageView.canvas,
            HeaderTitle = pageView.headerTitle,
            ScrollRect = pageView.scrollRect,
            CardListContent = pageView.cardListContent,
            EmptyHint = pageView.emptyHint,
        };

        if (pageView.headerTitle != null)
            pageView.headerTitle.text = headerTitle;

        if (pageView.emptyHint != null)
        {
            var emptyTmp = pageView.emptyHint.GetComponent<TMPro.TextMeshProUGUI>();
            if (emptyTmp != null && !string.IsNullOrWhiteSpace(emptyHint))
                emptyTmp.text = emptyHint;
        }

        if (pageView.backButton != null)
        {
            StoryFlowBackButtonUi.BindNavigation(pageView.backButton, "← 返回故事库", backSceneName);
            pageView.backButton.transform.SetAsLastSibling();
        }
        else
        {
            var backBtn = StoryFlowBackButtonUi.EnsureTopLeft(_ui.Canvas, "← 返回故事库", backSceneName);
            if (backBtn != null)
                backBtn.transform.SetAsLastSibling();
        }

        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    StoryLibraryUiBuilder.BuildOptions CreateBuildOptions() =>
        new StoryLibraryUiBuilder.BuildOptions
        {
            headerTitle = headerTitle,
            emptyHint = emptyHint,
            useStoryLibraryTitleBanner = false,
        };

    void RefreshCards()
    {
        if (!_uiBuilt || _ui?.CardListContent == null)
            return;

        for (int i = _ui.CardListContent.childCount - 1; i >= 0; i--)
            Destroy(_ui.CardListContent.GetChild(i).gameObject);

        var prefab = cardPrefab != null ? cardPrefab : StoryLibraryUiBuilder.LoadCardPrefab();
        if (prefab == null)
        {
            Debug.LogError("[CompletedStory] 未找到 StoryCard 预制体。请将 Assets/Prefabs/UI/StoryCard 复制到 Assets/Resources/UI/StoryCard。");
            if (_ui.EmptyHint != null)
                _ui.EmptyHint.SetActive(true);
            return;
        }

        var entries = CompletedStoryStore.LoadIndex();
        bool hasEntries = entries != null && entries.Length > 0;

        if (_ui.EmptyHint != null)
            _ui.EmptyHint.SetActive(!hasEntries);
        if (_ui.ScrollRect != null)
            _ui.ScrollRect.gameObject.SetActive(hasEntries);

        if (!hasEntries)
        {
            StoryLibraryUiBuilder.ResizeScrollContent(_ui.CardListContent, 0);
            return;
        }

        int count = 0;
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.saveId))
                continue;

            var card = Instantiate(prefab, _ui.CardListContent);
            card.gameObject.SetActive(true);
            var cover = CompletedStoryStore.LoadCoverSprite(entry.saveId, entry);
            string saveId = entry.saveId;
            card.BindCompletedStory(entry.title, cover, () =>
            {
                CompletedStoryContext.Select(saveId);
                SceneManager.LoadScene(viewerSceneName);
            }, cardButtonLabel);
            count++;
        }

        StoryLibraryUiBuilder.ResizeScrollContent(_ui.CardListContent, count);
    }
}
