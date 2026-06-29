using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>绘本阅读页 UI 搭建，供 Prefab / 场景可视化编辑。</summary>
public static class CompletedStoryViewerUiBuilder
{
    const float EdgePadding = 64f;
    const float BottomInset = 56f;
    const float ButtonSpacing = 36f;
    static readonly Vector2 NavButtonSize = new Vector2(200f, 200f);

    public static CompletedStoryViewerPageView BuildPageView(Transform parent)
    {
        CompletedStoryRuntimeUi.EnsureEventSystem();

        var canvasGo = new GameObject("CompletedStoryViewerCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CompletedStoryRuntimeUi.EnsureCanvasScaler(canvas);

        var view = canvasGo.AddComponent<CompletedStoryViewerPageView>();
        view.canvas = canvas;

        view.pageImage = CompletedStoryRuntimeUi.CreateFullScreenImage(canvas.transform, "PageImage");

        var reader = CompletedStoryRuntimeUi.CreateStoryReaderPanel(canvas.transform);
        view.storyReaderPanelRoot = reader.root;
        view.storyText = reader.storyText;
        view.recordButton = reader.recordButton;
        view.playButton = reader.playButton;
        view.rerecordButton = reader.rerecordButton;
        view.voiceStatusText = reader.statusText;
        reader.root.gameObject.SetActive(true);

        view.storyToggleButton = CompletedStoryRuntimeUi.CreateStoryToggleButton(canvas.transform);

        (view.prevPageButton, view.nextPageButton, view.pageIndicatorText) =
            CompletedStoryRuntimeUi.CreateBottomNav(
                canvas.transform,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                NavButtonSize,
                EdgePadding,
                BottomInset,
                ButtonSpacing);

        view.exitButton = StoryLibraryUiBuilder.CreateBackButton(canvas.transform);
        view.vrToggleButton = CreateTopBarButton(canvas.transform, "VrToggleButton", "沉浸 VR", 0);
        view.stereoToggleButton = CreateTopBarButton(canvas.transform, "StereoToggleButton", "立体分屏", 1);
        view.stereoToggleButton.gameObject.SetActive(false);
        view.vrHintText = CreateVrHint(canvas.transform);

        return view;
    }

    static Button CreateTopBarButton(Transform parent, string name, string label, int columnIndex)
    {
        const float width = 200f;
        const float height = 72f;
        const float margin = 28f;
        const float spacing = 16f;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(-margin - columnIndex * (width + spacing), -margin);

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(40, 44, 52, 255);
        text.text = label;
        return button;
    }

    static Text CreateVrHint(Transform parent)
    {
        var go = new GameObject("VrHint", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, 56f);
        rt.anchoredPosition = new Vector2(0f, -108f);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(255, 255, 255, 230);
        text.text = "";
        go.SetActive(false);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 160);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }
}
