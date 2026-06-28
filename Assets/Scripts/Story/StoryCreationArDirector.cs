using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 故事创作页 AR 助手：在摄像头画面上叠 2D 角色贴纸，并用孩子能懂的方式提示缺谁、谁到了。
/// </summary>
[DisallowMultipleComponent]
public class StoryCreationArDirector : MonoBehaviour
{
    const float StickerSize = 88f;
    const float MiniStickerScale = 0.55f;

    ArUcoDetector _detector;
    Font _font;
    RawImage _miniPreview;
    RawImage _expandedPreview;
    RectTransform _miniStickerRoot;
    RectTransform _expandedStickerRoot;

    RectTransform _rosterPanel;
    Text _rosterTitle;
    Text _rosterHint;
    Text _readyBanner;
    Transform _rosterListRoot;

    StoryDefinition.StoryPageDefinition _page;
    StoryDefinition.CharacterReferenceEntry[] _catalog;
    StoryMarkerTaxonomy _taxonomy = StoryMarkerTaxonomy.Default;

    readonly Dictionary<int, RectTransform> _miniStickers = new Dictionary<int, RectTransform>();
    readonly Dictionary<int, RectTransform> _expandedStickers = new Dictionary<int, RectTransform>();
    readonly Dictionary<int, RosterRow> _rosterRows = new Dictionary<int, RosterRow>();
    readonly List<int> _requiredIds = new List<int>();
    readonly HashSet<int> _seenCharacterIds = new HashSet<int>();
    readonly Dictionary<int, Vector2> _lastStickerPositions = new Dictionary<int, Vector2>();
    readonly Dictionary<int, float> _lastMoveReactTime = new Dictionary<int, float>();
    bool _readyAnnounced;

    const float MoveReactPixels = 72f;
    const float MoveReactCooldownSeconds = 8f;

    /// <summary>新识别到一名角色时（仅首次）。</summary>
    public event Action<string> CharacterArrived;

    /// <summary>角色在镜头里移动了较明显距离。</summary>
    public event Action<string> CharacterMoved;

    /// <summary>本页所需角色全部到齐时（仅首次）。</summary>
    public event Action AllCharactersReady;

    /// <summary>左侧名单提示变化时。</summary>
    public event Action<string> RosterHintChanged;
    bool _active;
    float _bobPhase;

    struct RosterRow
    {
        public GameObject root;
        public Image portrait;
        public Text nameText;
        public Text statusText;
    }

    public void Initialize(RectTransform canvasRoot, RawImage miniPreview, RawImage expandedPreview, ArUcoDetector detector, Font font)
    {
        _detector = detector;
        _font = font;
        _miniPreview = miniPreview;
        _expandedPreview = expandedPreview;

        BuildRosterPanel(canvasRoot);
        _miniStickerRoot = CreateStickerLayer(_miniPreview != null ? _miniPreview.transform : null, "MiniArStickers");
        _expandedStickerRoot = CreateStickerLayer(
            _expandedPreview != null ? _expandedPreview.transform.parent : null,
            "ExpandedArStickers");

        if (_rosterPanel != null)
            _rosterPanel.SetAsLastSibling();
    }

    public void SetPageContext(
        StoryDefinition.StoryPageDefinition page,
        StoryDefinition.CharacterReferenceEntry[] catalog,
        StoryMarkerTaxonomy taxonomy)
    {
        _page = page;
        _catalog = catalog;
        _taxonomy = taxonomy;

        _requiredIds.Clear();
        if (page?.requiredCharacterIds != null && page.requiredCharacterIds.Length > 0)
        {
            foreach (int id in page.requiredCharacterIds)
                _requiredIds.Add(id);
        }

        RebuildRosterRows();
        _seenCharacterIds.Clear();
        _lastStickerPositions.Clear();
        _lastMoveReactTime.Clear();
        _readyAnnounced = false;
        UpdateRosterHint("把积木摆进右上角镜头里");
        SetReadyBannerVisible(false);
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (_rosterPanel != null)
            _rosterPanel.gameObject.SetActive(active);
        if (!active)
        {
            ClearStickers(_miniStickers);
            ClearStickers(_expandedStickers);
        }
    }

    public void ShowSpeechBubble(string roleName, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return;

        string who = string.IsNullOrWhiteSpace(roleName) ? "故事伙伴" : roleName.Trim();
        UpdateRosterHint($"「{who}」说：{answer.Trim()}");
        ShowStickerSpeechBubble(who, answer.Trim());
    }

    public void ClearSpeechBubble()
    {
        RefreshFromDetector();
    }

    void Update()
    {
        if (!_active)
            return;

        _bobPhase += Time.deltaTime * 3f;
        RefreshFromDetector();
    }

    void RefreshFromDetector()
    {
        var detected = CollectCharacterMarkers();
        var detectedSet = new HashSet<int>();
        foreach (var marker in detected)
            detectedSet.Add(marker.id);

        UpdateRoster(detectedSet);
        UpdateStickers(_miniPreview, _miniStickerRoot, _miniStickers, detected, MiniStickerScale);
        UpdateStickers(_expandedPreview, _expandedStickerRoot, _expandedStickers, detected, 1f);
        NotifyCharacterMoves(detected);

        bool ready = IsLayoutReady(detectedSet);
        SetReadyBannerVisible(ready);
        NotifyPlacementEvents(detectedSet);

        if (ready)
            UpdateRosterHint("太棒了！伙伴都到齐啦，可以点「这页摆好了」");
        else if (_requiredIds.Count > 0)
            UpdateRosterHint(BuildMissingHint(detectedSet));
        else if (detectedSet.Count == 0)
            UpdateRosterHint("还没看到积木，把角色放进镜头吧");
        else
            UpdateRosterHint(BuildTogetherHint(detectedSet));
    }

    void NotifyPlacementEvents(HashSet<int> detectedSet)
    {
        foreach (int id in detectedSet)
        {
            if (!_taxonomy.IsCharacter(id) || _seenCharacterIds.Contains(id))
                continue;
            _seenCharacterIds.Add(id);
            CharacterArrived?.Invoke(ResolveRoleName(id));
        }

        if (!_readyAnnounced && IsLayoutReady(detectedSet))
        {
            _readyAnnounced = true;
            AllCharactersReady?.Invoke();
        }
    }

    void NotifyCharacterMoves(List<MarkerView> detected)
    {
        float now = Time.unscaledTime;
        foreach (var marker in detected)
        {
            if (!_lastStickerPositions.TryGetValue(marker.id, out var prev))
            {
                _lastStickerPositions[marker.id] = marker.pixel;
                continue;
            }

            float dist = Vector2.Distance(prev, marker.pixel);
            _lastStickerPositions[marker.id] = marker.pixel;
            if (dist < MoveReactPixels)
                continue;

            if (_lastMoveReactTime.TryGetValue(marker.id, out float last) &&
                now - last < MoveReactCooldownSeconds)
                continue;

            _lastMoveReactTime[marker.id] = now;
            CharacterMoved?.Invoke(marker.roleName);
        }

        var live = new HashSet<int>();
        foreach (var marker in detected)
            live.Add(marker.id);
        var stale = new List<int>();
        foreach (var id in _lastStickerPositions.Keys)
        {
            if (!live.Contains(id))
                stale.Add(id);
        }
        foreach (int id in stale)
        {
            _lastStickerPositions.Remove(id);
            _lastMoveReactTime.Remove(id);
        }
    }

    void ShowStickerSpeechBubble(string roleName, string text)
    {
        int markerId = ResolveMarkerIdByRole(roleName);
        if (markerId <= 0)
            return;

        ShowBubbleOnSticker(_miniStickers, markerId, text);
        ShowBubbleOnSticker(_expandedStickers, markerId, text);
    }

    static void ShowBubbleOnSticker(Dictionary<int, RectTransform> pool, int markerId, string text)
    {
        if (!pool.TryGetValue(markerId, out var sticker) || sticker == null)
            return;
        var bubble = sticker.Find("SpeechBubble");
        if (bubble == null)
            return;
        bubble.gameObject.SetActive(true);
        var label = bubble.Find("BubbleText")?.GetComponent<Text>();
        if (label != null)
            label.text = text;
    }

    int ResolveMarkerIdByRole(string roleName)
    {
        if (_catalog == null || string.IsNullOrWhiteSpace(roleName))
            return -1;
        foreach (var entry in _catalog)
        {
            if (entry != null && string.Equals(entry.roleName?.Trim(), roleName.Trim(), StringComparison.Ordinal))
                return entry.markerId;
        }
        return -1;
    }

    List<MarkerView> CollectCharacterMarkers()
    {
        var list = new List<MarkerView>();
        if (_detector?.DetectedMarkers == null)
            return list;

        foreach (var marker in _detector.DetectedMarkers)
        {
            if (!_taxonomy.IsCharacter(marker.id))
                continue;
            list.Add(new MarkerView
            {
                id = marker.id,
                pixel = marker.pixelPosition,
                roleName = ResolveRoleName(marker.id),
                sprite = ResolveSprite(marker.id),
            });
        }

        return list;
    }

    bool IsLayoutReady(HashSet<int> detectedSet)
    {
        if (_requiredIds.Count > 0)
        {
            foreach (int id in _requiredIds)
            {
                if (!detectedSet.Contains(id))
                    return false;
            }
            return detectedSet.Count > 0;
        }

        return detectedSet.Count > 0;
    }

    string BuildMissingHint(HashSet<int> detectedSet)
    {
        var missing = new List<string>();
        foreach (int id in _requiredIds)
        {
            if (!detectedSet.Contains(id))
                missing.Add(ResolveRoleName(id));
        }

        if (missing.Count == 0)
            return "再检查一下镜头里的摆放";
        if (missing.Count == 1)
            return $"还差 {missing[0]}，快把 ta 的积木放进镜头！";
        return $"还差 {string.Join("、", missing)}，把积木都摆进来吧";
    }

    string BuildTogetherHint(HashSet<int> detectedSet)
    {
        var names = new List<string>();
        foreach (int id in detectedSet)
            names.Add(ResolveRoleName(id));
        names.Sort();
        if (names.Count == 1)
            return $"{names[0]} 来啦！想想 ta 在做什么？";
        if (names.Count == 2)
            return $"{names[0]} 和 {names[1]} 都在镜头里啦！";
        return string.Join("、", names) + " 都准备好啦";
    }

    void UpdateRoster(HashSet<int> detectedSet)
    {
        foreach (var pair in _rosterRows)
        {
            int id = pair.Key;
            var row = pair.Value;
            bool found = detectedSet.Contains(id);
            if (row.statusText != null)
            {
                row.statusText.text = found ? "来啦 ✓" : "还没找到";
                row.statusText.color = found
                    ? new Color32(36, 142, 78, 255)
                    : new Color32(210, 92, 64, 255);
            }

            if (row.portrait != null)
            {
                row.portrait.color = found ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }

    void UpdateStickers(
        RawImage preview,
        RectTransform root,
        Dictionary<int, RectTransform> pool,
        List<MarkerView> markers,
        float scale)
    {
        if (root == null || preview == null || preview.texture == null)
        {
            ClearStickers(pool);
            return;
        }

        var live = new HashSet<int>();
        foreach (var marker in markers)
        {
            live.Add(marker.id);
            if (!pool.TryGetValue(marker.id, out var sticker))
            {
                sticker = CreateSticker(root, marker);
                pool[marker.id] = sticker;
            }

            PositionSticker(sticker, preview, marker.pixel, scale);
            ApplyStickerVisual(sticker, marker);
        }

        var toRemove = new List<int>();
        foreach (var id in pool.Keys)
        {
            if (!live.Contains(id))
                toRemove.Add(id);
        }

        foreach (int id in toRemove)
        {
            if (pool.TryGetValue(id, out var sticker) && sticker != null)
                Destroy(sticker.gameObject);
            pool.Remove(id);
        }
    }

    void PositionSticker(RectTransform sticker, RawImage preview, Vector2 pixelPos, float scale)
    {
        var local = MarkerPixelToLocal(pixelPos, preview);
        float bob = Mathf.Sin(_bobPhase + sticker.GetInstanceID() * 0.17f) * 4f * scale;
        sticker.localScale = Vector3.one * scale;
        sticker.anchoredPosition = local + new Vector2(0f, StickerSize * 0.55f * scale + bob);
    }

    static Vector2 MarkerPixelToLocal(Vector2 pixelPos, RawImage rawImage)
    {
        var rt = rawImage.rectTransform;
        var tex = rawImage.texture;
        if (tex == null)
            return Vector2.zero;

        float u = pixelPos.x / tex.width;
        float v = 1f - pixelPos.y / tex.height;
        var rect = rt.rect;
        return new Vector2(
            (u - rt.pivot.x) * rect.width,
            (v - rt.pivot.y) * rect.height);
    }

    RectTransform CreateSticker(RectTransform parent, MarkerView marker)
    {
        var rootGo = CreateUiObject(parent, $"Sticker_{marker.id}");
        var rt = rootGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(StickerSize, StickerSize + 34f);

        var portraitGo = CreateUiObject(rt, "Portrait");
        var portraitRt = portraitGo.GetComponent<RectTransform>();
        portraitRt.anchorMin = new Vector2(0.5f, 1f);
        portraitRt.anchorMax = new Vector2(0.5f, 1f);
        portraitRt.pivot = new Vector2(0.5f, 1f);
        portraitRt.sizeDelta = new Vector2(StickerSize, StickerSize);
        portraitRt.anchoredPosition = Vector2.zero;

        var frame = portraitGo.AddComponent<Image>();
        frame.color = new Color32(255, 255, 255, 235);

        var imageGo = CreateUiObject(portraitGo.transform, "Face");
        Stretch(imageGo.GetComponent<RectTransform>(), 6f);
        var face = imageGo.AddComponent<Image>();
        face.preserveAspect = true;
        face.raycastTarget = false;

        var labelGo = CreateUiObject(rt, "Label");
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0f);
        labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.sizeDelta = new Vector2(StickerSize + 24f, 28f);
        labelRt.anchoredPosition = Vector2.zero;

        var label = labelGo.AddComponent<Text>();
        label.font = _font;
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        var outline = labelGo.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 180);
        outline.effectDistance = new Vector2(1f, -1f);

        var bubbleGo = CreateUiObject(rt, "SpeechBubble");
        var bubbleRt = bubbleGo.GetComponent<RectTransform>();
        bubbleRt.anchorMin = new Vector2(0.5f, 1f);
        bubbleRt.anchorMax = new Vector2(0.5f, 1f);
        bubbleRt.pivot = new Vector2(0.5f, 0f);
        bubbleRt.sizeDelta = new Vector2(StickerSize + 80f, 72f);
        bubbleRt.anchoredPosition = new Vector2(0f, StickerSize * 0.2f);
        var bubbleBg = bubbleGo.AddComponent<Image>();
        bubbleBg.color = new Color32(255, 255, 255, 240);
        bubbleGo.SetActive(false);

        var bubbleTextGo = CreateUiObject(bubbleGo.transform, "BubbleText");
        Stretch(bubbleTextGo.GetComponent<RectTransform>(), 8f);
        var bubbleText = bubbleTextGo.AddComponent<Text>();
        bubbleText.font = _font;
        bubbleText.fontSize = 18;
        bubbleText.alignment = TextAnchor.MiddleCenter;
        bubbleText.color = new Color32(40, 44, 52, 255);
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
        bubbleText.raycastTarget = false;

        rootGo.AddComponent<CanvasGroup>().blocksRaycasts = false;
        return rt;
    }

    void ApplyStickerVisual(RectTransform sticker, MarkerView marker)
    {
        var face = sticker.Find("Portrait/Face")?.GetComponent<Image>();
        if (face != null)
        {
            face.sprite = marker.sprite;
            face.enabled = marker.sprite != null;
        }

        var label = sticker.Find("Label")?.GetComponent<Text>();
        if (label != null)
            label.text = marker.roleName;
    }

    void BuildRosterPanel(RectTransform canvasRoot)
    {
        _rosterPanel = CreateUiObject(canvasRoot, "ArRosterPanel").GetComponent<RectTransform>();
        _rosterPanel.anchorMin = new Vector2(0f, 1f);
        _rosterPanel.anchorMax = new Vector2(0f, 1f);
        _rosterPanel.pivot = new Vector2(0f, 1f);
        _rosterPanel.sizeDelta = new Vector2(200f, 280f);
        _rosterPanel.anchoredPosition = new Vector2(16f, -96f);

        var bg = _rosterPanel.gameObject.AddComponent<Image>();
        bg.color = new Color32(255, 255, 255, 230);
        bg.raycastTarget = false;

        _rosterTitle = CreateText(_rosterPanel, "Title", "伙伴", 22, FontStyle.Bold, TextAnchor.UpperCenter);
        var titleRt = _rosterTitle.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-24f, 44f);
        titleRt.anchoredPosition = new Vector2(0f, -12f);

        _readyBanner = CreateText(_rosterPanel, "ReadyBanner", "", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        var readyRt = _readyBanner.rectTransform;
        readyRt.anchorMin = new Vector2(0f, 1f);
        readyRt.anchorMax = new Vector2(1f, 1f);
        readyRt.pivot = new Vector2(0.5f, 1f);
        readyRt.sizeDelta = new Vector2(-20f, 56f);
        readyRt.anchoredPosition = new Vector2(0f, -52f);
        _readyBanner.color = new Color32(36, 142, 78, 255);
        _readyBanner.horizontalOverflow = HorizontalWrapMode.Wrap;
        _readyBanner.gameObject.SetActive(false);

        var listGo = CreateUiObject(_rosterPanel, "List");
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 0f);
        listRt.anchorMax = new Vector2(1f, 1f);
        listRt.offsetMin = new Vector2(8f, 48f);
        listRt.offsetMax = new Vector2(-8f, -72f);
        _rosterListRoot = listGo.transform;

        _rosterHint = CreateText(_rosterPanel, "Hint", "", 20, FontStyle.Normal, TextAnchor.UpperCenter);
        var hintRt = _rosterHint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(-12f, 56f);
        hintRt.anchoredPosition = new Vector2(0f, 8f);
        _rosterHint.fontSize = 18;
        _rosterHint.color = new Color32(70, 76, 90, 255);
        _rosterHint.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    void RebuildRosterRows()
    {
        foreach (var pair in _rosterRows)
        {
            if (pair.Value.root != null)
                Destroy(pair.Value.root);
        }
        _rosterRows.Clear();

        var ids = _requiredIds.Count > 0 ? _requiredIds : BuildFallbackIds();
        float y = 0f;
        const float rowHeight = 72f;
        foreach (int id in ids)
        {
            var row = CreateRosterRow(id, y);
            _rosterRows[id] = row;
            y -= rowHeight;
        }
    }

    List<int> BuildFallbackIds()
    {
        var ids = new List<int>();
        if (_catalog == null)
            return ids;
        foreach (var entry in _catalog)
        {
            if (entry != null && _taxonomy.IsCharacter(entry.markerId))
                ids.Add(entry.markerId);
        }
        ids.Sort();
        return ids;
    }

    RosterRow CreateRosterRow(int markerId, float y)
    {
        var go = CreateUiObject(_rosterListRoot, $"Roster_{markerId}");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, 84f);
        rt.anchoredPosition = new Vector2(0f, y);

        var portraitGo = CreateUiObject(go.transform, "Portrait");
        var portraitRt = portraitGo.GetComponent<RectTransform>();
        portraitRt.anchorMin = new Vector2(0f, 0.5f);
        portraitRt.anchorMax = new Vector2(0f, 0.5f);
        portraitRt.pivot = new Vector2(0f, 0.5f);
        portraitRt.sizeDelta = new Vector2(64f, 64f);
        portraitRt.anchoredPosition = new Vector2(0f, 0f);
        var portrait = portraitGo.AddComponent<Image>();
        portrait.sprite = ResolveSprite(markerId);
        portrait.preserveAspect = true;
        portrait.color = new Color(1f, 1f, 1f, 0.35f);

        var nameText = CreateText(go.transform, "Name", ResolveRoleName(markerId), 24, FontStyle.Bold, TextAnchor.UpperLeft);
        var nameRt = nameText.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(76f, -34f);
        nameRt.offsetMax = new Vector2(0f, -4f);
        nameText.alignment = TextAnchor.UpperLeft;
        nameText.color = new Color32(40, 44, 52, 255);

        var statusText = CreateText(go.transform, "Status", "还没找到", 20, FontStyle.Normal, TextAnchor.UpperLeft);
        var statusRt = statusText.rectTransform;
        statusRt.anchorMin = new Vector2(0f, 0f);
        statusRt.anchorMax = new Vector2(1f, 0f);
        statusRt.offsetMin = new Vector2(76f, 8f);
        statusRt.offsetMax = new Vector2(0f, 32f);
        statusText.alignment = TextAnchor.UpperLeft;
        statusText.color = new Color32(210, 92, 64, 255);

        return new RosterRow
        {
            root = go,
            portrait = portrait,
            nameText = nameText,
            statusText = statusText,
        };
    }

    void SetReadyBannerVisible(bool visible)
    {
        if (_readyBanner == null)
            return;
        _readyBanner.gameObject.SetActive(visible);
        _readyBanner.text = visible ? "可以生成啦！" : "";
    }

    void UpdateRosterHint(string text)
    {
        if (_rosterHint != null)
            _rosterHint.text = text ?? "";
        RosterHintChanged?.Invoke(text ?? "");
    }

    string ResolveRoleName(int markerId)
    {
        if (_catalog != null)
        {
            foreach (var entry in _catalog)
            {
                if (entry != null && entry.markerId == markerId && !string.IsNullOrWhiteSpace(entry.roleName))
                    return entry.roleName.Trim();
            }
        }
        return $"伙伴{markerId}";
    }

    Sprite ResolveSprite(int markerId)
    {
        if (_catalog == null)
            return null;
        foreach (var entry in _catalog)
        {
            if (entry != null && entry.markerId == markerId)
                return entry.referenceSprite;
        }
        return null;
    }

    static RectTransform CreateStickerLayer(Transform parent, string name)
    {
        if (parent == null)
            return null;

        var go = CreateUiObject(parent, name);
        var rt = go.GetComponent<RectTransform>();
        Stretch(rt, 0f);
        go.AddComponent<CanvasGroup>().blocksRaycasts = false;
        return rt;
    }

    static void ClearStickers(Dictionary<int, RectTransform> pool)
    {
        foreach (var sticker in pool.Values)
        {
            if (sticker != null)
                Destroy(sticker.gameObject);
        }
        pool.Clear();
    }

    static GameObject CreateUiObject(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    Text CreateText(Transform parent, string name, string value, int size, FontStyle style, TextAnchor anchor)
    {
        var go = CreateUiObject(parent, name);
        var text = go.AddComponent<Text>();
        text.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    struct MarkerView
    {
        public int id;
        public Vector2 pixel;
        public string roleName;
        public Sprite sprite;
    }
}
