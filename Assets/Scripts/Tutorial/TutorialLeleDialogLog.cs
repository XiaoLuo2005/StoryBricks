using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>乐乐面板对话历史：ScrollRect + Content/Output，可滑动查看。</summary>
[DisallowMultipleComponent]
public class TutorialLeleDialogLog : MonoBehaviour
{
    ScrollRect _scroll;
    RectTransform _viewport;
    RectTransform _content;
    TextMeshProUGUI _output;
    TMP_FontAsset _font;
    bool _stickToBottom = true;

    public bool HasMessages => _output != null && !string.IsNullOrWhiteSpace(_output.text);

    public void Initialize(ScrollRect scroll, TMP_FontAsset font, TextMeshProUGUI preferredOutput = null)
    {
        _scroll = scroll;
        _font = font != null ? font : TutorialUiArt.Font;
        if (_scroll == null)
        {
            Debug.LogError("[TutorialLeleDialogLog] dialogScroll 为空");
            return;
        }

        _viewport = _scroll.viewport;
        _content = _scroll.content;
        if (_viewport == null || _content == null)
        {
            Debug.LogError("[TutorialLeleDialogLog] ScrollRect viewport/content 未配置");
            return;
        }

        FixViewportMask(_viewport);
        SetupScroll();
        _output = ResolveOutput(preferredOutput);
        if (_output == null)
        {
            Debug.LogError("[TutorialLeleDialogLog] 无法创建 Output 文本");
            return;
        }

        PlaceOutputInContent(_output);
        ConfigureOutput(_output);

        _scroll.onValueChanged.RemoveListener(OnScrollChanged);
        _scroll.onValueChanged.AddListener(OnScrollChanged);
        _stickToBottom = true;
    }

    public void Clear()
    {
        if (_output == null)
            return;
        _output.text = "";
        RefreshLayout(scrollToLatest: true);
    }

    public void SetOpening(string text)
    {
        Clear();
        if (!string.IsNullOrWhiteSpace(text))
            AppendLele(text.Trim());
    }

    public void AppendUser(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        AppendSection("你", text.Trim());
    }

    public void AppendLele(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        AppendSection(LeleVoiceAssistant.DisplayName, text.Trim());
    }

    public void AppendSystem(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var prefix = _output.text.Length > 0 ? "\n\n" : "";
        AppendRaw($"{prefix}[提示] {text.Trim()}");
    }

    public string ExportPlainText() => _output != null ? _output.text : "";

    public void ScrollToLatestIfNeeded() => RefreshLayout(_stickToBottom);

    static void FixViewportMask(RectTransform viewport)
    {
        var legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(legacyMask);
            else
#endif
                Object.Destroy(legacyMask);
        }

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        var img = viewport.GetComponent<Image>();
        if (img == null)
            img = viewport.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.02f);
        img.raycastTarget = true;
    }

    void SetupScroll()
    {
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Elastic;
        _scroll.scrollSensitivity = 28f;
        _scroll.inertia = true;

        var scrollRt = _scroll.GetComponent<RectTransform>();
        EnsureRaycastTarget(scrollRt);

        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;
    }

    static void EnsureRaycastTarget(RectTransform rt)
    {
        if (rt == null)
            return;
        var img = rt.GetComponent<Image>();
        if (img == null)
            img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.001f);
        img.raycastTarget = true;
    }

    TextMeshProUGUI ResolveOutput(TextMeshProUGUI preferredOutput)
    {
        if (preferredOutput != null && preferredOutput.gameObject != null)
            return preferredOutput;

        var legacy = _content.Find("Output");
        if (legacy != null)
        {
            var tmp = legacy.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                return tmp;
        }

        var go = new GameObject("Output", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(_content, false);
        return go.AddComponent<TextMeshProUGUI>();
    }

    void PlaceOutputInContent(TextMeshProUGUI tmp)
    {
        var rt = tmp.rectTransform;
        rt.SetParent(_content, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, -8f);
        rt.sizeDelta = new Vector2(-16f, 0f);
        rt.localScale = Vector3.one;
        tmp.gameObject.SetActive(true);
    }

    void ConfigureOutput(TextMeshProUGUI tmp)
    {
        var font = _font != null ? _font : tmp.font;
        if (font == null)
            font = TutorialUiArt.Font;
        if (font != null)
            tmp.font = font;

        tmp.fontSize = 22;
        tmp.color = TutorialUiArt.BodyBrown;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = false;

        var csf = tmp.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = tmp.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void AppendSection(string role, string body)
    {
        var prefix = _output.text.Length > 0 ? "\n\n" : "";
        AppendRaw($"{prefix}{role}\n{body}");
    }

    void AppendRaw(string chunk)
    {
        if (_output == null || string.IsNullOrEmpty(chunk))
            return;

        _output.text += chunk;
        RefreshLayout(scrollToLatest: true);
    }

    void OnScrollChanged(Vector2 pos) => _stickToBottom = pos.y <= 0.05f;

    void RefreshLayout(bool scrollToLatest)
    {
        if (_scroll == null || _content == null || _output == null)
            return;

        float width = _viewport != null ? _viewport.rect.width - 16f : 300f;
        if (width < 80f)
            width = 300f;

        _output.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _output.ForceMeshUpdate();

        float textH = Mathf.Max(_output.preferredHeight + 16f, 32f);
        float viewH = _viewport != null ? _viewport.rect.height : textH;
        _content.sizeDelta = new Vector2(0f, Mathf.Max(textH, viewH));

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        Canvas.ForceUpdateCanvases();

        _scroll.vertical = textH > viewH + 2f;

        if (!scrollToLatest)
            return;

        _scroll.StopMovement();
        // 文字从 Content 顶部向下增长；短内容滚到顶，长内容滚到底看最新
        _scroll.verticalNormalizedPosition = textH > viewH + 2f ? 0f : 1f;
    }
}
