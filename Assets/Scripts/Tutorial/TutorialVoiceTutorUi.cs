using UnityEngine;

/// <summary>在教程 Canvas 右侧挂载 AI 助教面板。</summary>
public static class TutorialVoiceTutorUi
{
    /// <param name="bottomBarHeight">底栏高度，用于面板上下留白</param>
    public static void TryBuild(
        RectTransform canvasRoot,
        TutorialStepsConfig config,
        StepViewerUI viewer,
        string gatewayBaseUrl,
        Font uiFont,
        bool enable,
        float topBarHeight,
        float bottomBarHeight)
    {
        if (!enable || string.IsNullOrWhiteSpace(gatewayBaseUrl) || config == null || viewer == null)
            return;

        var go = new GameObject("VoiceTutorRoot", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRoot, false);
        rt.SetAsLastSibling();

        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        const float panelW = 368f;
        var vMargin = topBarHeight + bottomBarHeight + 40f;
        rt.sizeDelta = new Vector2(panelW, -vMargin);
        rt.anchoredPosition = new Vector2(-20f, (bottomBarHeight - topBarHeight) * 0.5f);

        var ctrl = go.AddComponent<TutorialVoiceTutorController>();
        ctrl.Initialize(config, viewer, gatewayBaseUrl, uiFont);
        ctrl.BuildPanel(rt);
    }
}
