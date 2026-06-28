using TMPro;
using UnityEngine;

/// <summary>绑定 Prefab 里可视化编辑的乐乐面板。</summary>
public static class TutorialVoiceTutorUi
{
    public static void TryBuild(
        RectTransform lelePanelRoot,
        TutorialLelePanelView lelePanel,
        TutorialStepsConfig config,
        StepViewerUI viewer,
        string gatewayBaseUrl,
        TMP_FontAsset font,
        bool enable)
    {
        if (!enable || string.IsNullOrWhiteSpace(gatewayBaseUrl) || config == null || viewer == null)
            return;

        if (lelePanelRoot == null)
        {
            Debug.LogWarning("[TutorialVoiceTutorUi] 未指定 lelePanelRoot，跳过乐乐面板。");
            return;
        }

        var panelView = lelePanel;
        if (panelView == null)
            panelView = lelePanelRoot.GetComponent<TutorialLelePanelView>();

        if (panelView == null || !panelView.IsComplete)
        {
            Debug.LogWarning(
                "[TutorialVoiceTutorUi] Prefab 里缺少乐乐 UI。请运行 StoryBricks/教程/创建或修复 TutorialStepsPage Prefab。");
            panelView = TutorialLelePanelUiBuilder.Build(lelePanelRoot);
        }

        var ctrl = lelePanelRoot.GetComponent<TutorialVoiceTutorController>();
        if (ctrl == null)
            ctrl = lelePanelRoot.gameObject.AddComponent<TutorialVoiceTutorController>();

        ctrl.Initialize(config, viewer, gatewayBaseUrl, font != null ? font : TutorialUiArt.Font);
        ctrl.BindPanel(panelView);
    }
}
