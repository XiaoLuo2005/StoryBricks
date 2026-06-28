using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 积木搭建教程页：绑定 TutorialStepsPageView（Prefab/场景）与步骤数据、乐乐、3D 预览。
/// </summary>
[DefaultExecutionOrder(0)]
public class TutorialStepsPageBootstrap : MonoBehaviour
{
    public TutorialStepsConfig config;

    [Header("UI")]
    [Tooltip("拖入场景里的 TutorialCanvas，或留空并在下方指定 Prefab")]
    public TutorialStepsPageView pageView;
    [Tooltip("留空且 pageView 为空时，从 Resources 加载默认 Prefab")]
    public TutorialStepsPageView pageViewPrefab;
    [Tooltip("仅在 pageView 与 pageViewPrefab 都为空时，运行时临时搭建 UI（不可视化编辑）")]
    public bool allowRuntimeFallbackUi = true;

    [Header("乐乐")]
    public bool enableVoiceTutor = true;
    public string tutorGatewayBaseUrl = "http://127.0.0.1:8787";

    TutorialPreview3DOverlay _previewOverlay;

    void Awake()
    {
        if (config == null || config.steps == null || config.steps.Length == 0)
        {
            Debug.LogError("TutorialStepsPageBootstrap: 请在 Inspector 指定 TutorialStepsConfig，且 steps 非空。");
            return;
        }

        EnsureEventSystem();
        EnsurePageView();
        if (pageView == null || !pageView.IsComplete)
        {
            Debug.LogError("TutorialStepsPageBootstrap: 未找到可用的 TutorialStepsPageView。");
            return;
        }

        BindPageView();
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void EnsurePageView()
    {
        if (pageView != null && pageView.IsComplete)
            return;

        if (pageViewPrefab == null)
            pageViewPrefab = Resources.Load<TutorialStepsPageView>("UI/TutorialStepsPage");

        if (pageViewPrefab != null)
        {
            pageView = Instantiate(pageViewPrefab);
            pageView.name = pageViewPrefab.name;
            return;
        }

        if (!allowRuntimeFallbackUi)
            return;

        Debug.LogWarning(
            "[TutorialStepsPageBootstrap] 未配置 pageView / Prefab，正在运行时临时搭建 UI。" +
            "请运行菜单 StoryBricks/教程/创建 TutorialStepsPage UI Prefab。");
        pageView = TutorialStepsPageUiBuilder.Build(null);
    }

    void BindPageView()
    {
        if (pageView.titleText != null)
            pageView.titleText.text = config.title;

        var backLabel = StorySelectionContext.HasStoryWorks ? "← 返回作品集" : "← 返回积木库";
        SetButtonLabel(pageView.backButton, backLabel);
        pageView.backButton.onClick.RemoveAllListeners();
        pageView.backButton.onClick.AddListener(OnBackClicked);

        if (config.previewModelPrefab != null && pageView.preview3DButton != null)
        {
            pageView.preview3DButton.gameObject.SetActive(true);
            _previewOverlay = pageView.canvas.gameObject.GetComponent<TutorialPreview3DOverlay>();
            if (_previewOverlay == null)
                _previewOverlay = pageView.canvas.gameObject.AddComponent<TutorialPreview3DOverlay>();
            _previewOverlay.Configure(config.previewModelPrefab, TutorialStepsPageUiBuilder.BuiltinUIFont);
            pageView.preview3DButton.onClick.RemoveAllListeners();
            pageView.preview3DButton.onClick.AddListener(_previewOverlay.Open);
        }
        else if (pageView.preview3DButton != null)
        {
            pageView.preview3DButton.gameObject.SetActive(false);
        }

        var viewer = pageView.stepViewer;
        viewer.steps = config.steps;
        viewer.stepHints = ResolveStepHints();
        viewer.stepTutorDetails = ResolveStepTutorDetails();

        pageView.prevButton.onClick.RemoveAllListeners();
        pageView.nextButton.onClick.RemoveAllListeners();
        pageView.prevButton.onClick.AddListener(viewer.PrevStep);
        pageView.nextButton.onClick.AddListener(viewer.NextStep);

        var swipe = pageView.stepSwipeZone.GetComponent<SwipeStepNavigator>();
        if (swipe == null)
            swipe = pageView.stepSwipeZone.gameObject.AddComponent<SwipeStepNavigator>();
        swipe.viewer = viewer;

        TutorialMascotView.TryAddToCanvas(pageView.mascotRoot, config.mascotLottieJsonText);

        TutorialVoiceTutorUi.TryBuild(
            pageView.lelePanelRoot,
            pageView.lelePanel,
            config,
            viewer,
            tutorGatewayBaseUrl,
            TutorialUiArt.Font,
            enableVoiceTutor);
    }

    void OnBackClicked()
    {
        var scene = config != null
            ? StorySelectionContext.ResolvePortfolioReturnScene(config.portfolioSceneName)
            : StorySelectionContext.ResolvePortfolioReturnScene(StoryFlowScenes.BrickLibrary);
        SceneManager.LoadScene(scene);
    }

    string[] ResolveStepHints()
    {
        int stepCount = config.steps.Length;
        var hints = config.stepHints;
        if (stepCount > 0 && config.stepHintsSourceText != null &&
            !string.IsNullOrWhiteSpace(config.stepHintsSourceText.text))
        {
            var parsed = TutorialTutorSourceText.ParseStepHintsLines(config.stepHintsSourceText.text, stepCount);
            if (parsed != null)
                hints = parsed;
        }
        return hints;
    }

    TutorialStepTutorDetail[] ResolveStepTutorDetails()
    {
        int stepCount = config.steps.Length;
        var details = config.stepTutorDetails;
        if (stepCount > 0 && config.stepTutorDetailsSourceText != null &&
            !string.IsNullOrWhiteSpace(config.stepTutorDetailsSourceText.text))
        {
            var parsed = TutorialTutorSourceText.ParseStepDetailBlocks(config.stepTutorDetailsSourceText.text, stepCount);
            if (parsed != null)
                details = parsed;
        }
        return details;
    }

    static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;
        var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        var text = button.GetComponentInChildren<Text>();
        if (text != null)
            text.text = label;
    }
}
