using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>生成可在 Prefab 里拖拽的乐乐面板（无 LayoutGroup，Pos 不被锁定）。</summary>
public static class TutorialLelePanelUiBuilder
{
    public static TutorialLelePanelView Build(RectTransform leleRoot, bool clearExisting = true)
    {
        if (leleRoot == null)
            return null;

        if (clearExisting)
            ClearLeleRoot(leleRoot);

        var font = TutorialUiArt.Font;
        var view = leleRoot.GetComponent<TutorialLelePanelView>();
        if (view == null)
            view = leleRoot.gameObject.AddComponent<TutorialLelePanelView>();

        view.panelBackground = CreatePanelBackground(leleRoot);
        view.titleText = CreateTitle(leleRoot, font);
        view.dialogScroll = CreateDialogScroll(leleRoot, font, out var dialogOutput);
        view.dialogOutput = dialogOutput;
        view.listenStatusLabel = CreateListenStatus(leleRoot, font);
        view.statusText = CreateStatus(leleRoot, font);

        return view;
    }

    public static void ClearLeleRoot(RectTransform leleRoot)
    {
        for (var i = leleRoot.childCount - 1; i >= 0; i--)
            DestroyObject(leleRoot.GetChild(i).gameObject);

        RemoveComponentIfExists<Image>(leleRoot.gameObject);
        RemoveComponentIfExists<VerticalLayoutGroup>(leleRoot.gameObject);
        RemoveComponentIfExists<HorizontalLayoutGroup>(leleRoot.gameObject);
        RemoveComponentIfExists<LayoutElement>(leleRoot.gameObject);
    }

    static Image CreatePanelBackground(RectTransform parent)
    {
        var bg = CreateUiObject<Image>(parent, "PanelBackground");
        TutorialStepsPageUiBuilder.StretchFull(bg.rectTransform);
        TutorialUiArt.ApplyLelePanelBackground(bg);
        bg.raycastTarget = true;
        return bg;
    }

    static TextMeshProUGUI CreateTitle(RectTransform parent, TMP_FontAsset font)
    {
        var title = CreateUiLabel(parent, "Title", LeleVoiceAssistant.DisplayName, 30, TextAlignmentOptions.Left, FontStyles.Bold);
        var rt = title.rectTransform;
        rt.anchorMin = new Vector2(0.06f, 1f);
        rt.anchorMax = new Vector2(0.94f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 44f);
        rt.anchoredPosition = new Vector2(0f, -18f);
        if (font != null)
            title.font = font;
        return title;
    }

    static ScrollRect CreateDialogScroll(RectTransform parent, TMP_FontAsset font, out TextMeshProUGUI output)
    {
        var scrollGo = CreateUiChild(parent, "DialogScroll");
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(14f, 96f);
        scrollRt.offsetMax = new Vector2(-14f, -118f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateUiChild(scrollGo.transform, "Viewport").GetComponent<RectTransform>();
        TutorialStepsPageUiBuilder.StretchFull(viewport);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.12f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiChild(viewport, "Content").GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 0f);
        content.anchoredPosition = Vector2.zero;

        output = CreateOutputText(content, font);
        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }

    static TextMeshProUGUI CreateOutputText(RectTransform content, TMP_FontAsset font)
    {
        var outputGo = CreateUiChild(content, "Output");
        var rt = outputGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, 0f);
        rt.anchoredPosition = Vector2.zero;

        var text = outputGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = 22;
        text.color = TutorialUiArt.BodyBrown;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.text =
            $"你好！我是{LeleVoiceAssistant.DisplayName}。先说「{LeleVoiceAssistant.WakePhrase}」唤醒我，再提问。";

        var csf = outputGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return text;
    }

    static TextMeshProUGUI CreateListenStatus(RectTransform parent, TMP_FontAsset font)
    {
        var listenGo = CreateUiChild(parent, "ListenStatus");
        var listenRt = listenGo.GetComponent<RectTransform>();
        listenRt.anchorMin = new Vector2(0.08f, 0f);
        listenRt.anchorMax = new Vector2(0.92f, 0f);
        listenRt.pivot = new Vector2(0.5f, 0f);
        listenRt.sizeDelta = new Vector2(0f, 56f);
        listenRt.anchoredPosition = new Vector2(0f, 52f);

        var listenImg = listenGo.AddComponent<Image>();
        TutorialUiArt.ApplyButtonBackground(listenImg);
        listenGo.AddComponent<Button>().interactable = false;

        var label = CreateUiLabel(
            listenGo.transform,
            "ListenLabel",
            LeleVoiceAssistant.WakeHint,
            24,
            TextAlignmentOptions.Center);
        TutorialStepsPageUiBuilder.StretchFull(label.rectTransform);
        if (font != null)
            label.font = font;
        return label;
    }

    static TextMeshProUGUI CreateStatus(RectTransform parent, TMP_FontAsset font)
    {
        var status = CreateUiLabel(parent, "Status", "", 20, TextAlignmentOptions.Left);
        status.color = TutorialUiArt.MutedBrown;
        var rt = status.rectTransform;
        rt.anchorMin = new Vector2(0.08f, 0f);
        rt.anchorMax = new Vector2(0.92f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, 28f);
        rt.anchoredPosition = new Vector2(0f, 12f);
        if (font != null)
            status.font = font;
        return status;
    }

    static TextMeshProUGUI CreateUiLabel(
        Transform parent,
        string name,
        string content,
        float fontSize,
        TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var tmp = TutorialUiArt.CreateLabel(parent, name, content, fontSize, align, TutorialUiArt.BodyBrown, style);
        TutorialStepsPageUiBuilder.StretchFull(tmp.rectTransform);
        return tmp;
    }

    static GameObject CreateUiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        return go;
    }

    static T CreateUiObject<T>(Transform parent, string name) where T : Component
    {
        return CreateUiChild(parent, name).AddComponent<T>();
    }

    static void DestroyObject(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(go);
        else
#endif
            Object.Destroy(go);
    }

    static void RemoveComponentIfExists<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(c);
        else
#endif
            Object.Destroy(c);
    }
}
