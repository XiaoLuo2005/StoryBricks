using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 全屏半透明层 + RenderTexture 相机展示整模；拖拽旋转。由教程顶栏「3D 预览」打开。
/// </summary>
[DisallowMultipleComponent]
public class TutorialPreview3DOverlay : MonoBehaviour
{
    GameObject _prefab;
    Font _font;

    GameObject _modal;
    GameObject _worldRoot;
    Camera _cam;
    RenderTexture _rt;

    const int RtShortSide = 1024;

    public void Configure(GameObject modelPrefab, Font uiFont)
    {
        _prefab = modelPrefab;
        _font = uiFont;
    }

    public void Open()
    {
        if (_prefab == null)
            return;
        Close();

        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("TutorialPreview3DOverlay 需挂在带 Canvas 的物体上。");
            return;
        }

        _modal = new GameObject("Preview3DModal", typeof(RectTransform));
        _modal.layer = LayerMask.NameToLayer("UI");
        var modalRt = _modal.GetComponent<RectTransform>();
        modalRt.SetParent(canvas.transform, false);
        modalRt.SetAsLastSibling();
        StretchFull(modalRt);

        var dim = _modal.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        var holder = new GameObject("Content", typeof(RectTransform));
        holder.layer = LayerMask.NameToLayer("UI");
        var holderRt = holder.GetComponent<RectTransform>();
        holderRt.SetParent(modalRt, false);
        holderRt.anchorMin = new Vector2(0.5f, 0.5f);
        holderRt.anchorMax = new Vector2(0.5f, 0.5f);
        holderRt.pivot = new Vector2(0.5f, 0.5f);
        holderRt.sizeDelta = new Vector2(1680f, 920f);

        var title = CreateUiLabel(holderRt, "Title", "成品 3D 预览（拖拽旋转）", 32, TextAnchor.UpperCenter);
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 48f);
        titleRt.anchoredPosition = new Vector2(0f, -8f);

        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        int h = RtShortSide;
        int w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), RtShortSide, 2048);
        _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        _rt.Create();

        var rawGo = new GameObject("RawView", typeof(RectTransform));
        rawGo.layer = LayerMask.NameToLayer("UI");
        var rawRt = rawGo.GetComponent<RectTransform>();
        rawRt.SetParent(holderRt, false);
        rawRt.anchorMin = new Vector2(0.06f, 0.12f);
        rawRt.anchorMax = new Vector2(0.94f, 0.88f);
        rawRt.offsetMin = Vector2.zero;
        rawRt.offsetMax = Vector2.zero;

        var raw = rawGo.AddComponent<RawImage>();
        raw.texture = _rt;
        raw.raycastTarget = true;
        raw.color = Color.white;
        // RenderTexture 与 UI 纹理 V 轴相反，不翻转常会上下颠倒
        raw.uvRect = new Rect(0f, 1f, 1f, -1f);

        var closeBtn = CreateTextButton(holderRt, "CloseButton", "关闭", new Vector2(200f, 64f));
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 16f);
        closeBtn.onClick.AddListener(Close);

        var camGo = new GameObject("Preview3DCamera");
        _cam = camGo.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color32(248, 249, 252, 255);
        _cam.orthographic = false;
        _cam.fieldOfView = 36f;
        _cam.nearClipPlane = 0.02f;
        _cam.farClipPlane = 200f;
        _cam.targetTexture = _rt;
        int uiLayer = LayerMask.NameToLayer("UI");
        _cam.cullingMask = uiLayer >= 0 ? ~(1 << uiLayer) : ~0;

        // 必须独立于 Canvas：Overlay Canvas 的 RectTransform 会带着非均匀 scale，
        // 把子物体上的透视相机和网格一起压扁/错位，造成看不见或取景异常。
        _worldRoot = new GameObject("Preview3DWorld");
        _worldRoot.transform.SetParent(null, false);
        _worldRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _worldRoot.transform.localScale = Vector3.one;

        camGo.transform.SetParent(_worldRoot.transform, false);

        var lightGo = new GameObject("PreviewDirLight");
        lightGo.transform.SetParent(_worldRoot.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
        var dir = lightGo.AddComponent<Light>();
        dir.type = LightType.Directional;
        dir.intensity = 1.15f;

        var pivotGo = new GameObject("OrbitPivot");
        pivotGo.transform.SetParent(_worldRoot.transform, false);
        pivotGo.transform.localPosition = Vector3.zero;
        pivotGo.transform.localRotation = Quaternion.identity;
        var pivot = pivotGo.transform;

        var instance = Instantiate(_prefab, pivot);
        instance.hideFlags = HideFlags.HideAndDontSave;

        RecenterModelAtOrigin(instance.transform);

        var bounds = ComputeWorldBounds(pivot);
        float radius = bounds.extents.magnitude;
        if (radius < 1e-4f)
            radius = 0.5f;

        FramePerspectiveCamera(_cam, bounds, padding: 1.22f);

        var orbit = rawGo.AddComponent<TutorialPreviewOrbitDrag>();
        orbit.target = pivot;
        orbit.referenceCamera = _cam;
    }

    static void RecenterModelAtOrigin(Transform modelRoot)
    {
        var b = ComputeWorldBounds(modelRoot);
        if (b.size.sqrMagnitude <= 1e-12f)
            return;
        modelRoot.position -= b.center;
    }

    static Bounds ComputeWorldBounds(Transform root)
    {
        var rs = root.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
            return new Bounds(root.position, Vector3.one * 0.2f);

        var b = rs[0].bounds;
        for (var i = 1; i < rs.Length; i++)
            b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>
    /// 世界空间下使相机对准包围盒中心，距离随垂直 FOV 与半径自适应。
    /// </summary>
    static void FramePerspectiveCamera(Camera cam, Bounds worldBounds, float padding)
    {
        Vector3 center = worldBounds.center;
        float radius = worldBounds.extents.magnitude;
        if (radius < 1e-4f)
            radius = 0.5f;

        float vFovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float dist = radius / Mathf.Max(1e-4f, Mathf.Sin(vFovRad * 0.5f));
        dist *= padding;

        // 略抬高并从前上方看向中心，避免完全水平看不清顶面
        Vector3 dir = new Vector3(0.22f, 0.38f, 1f).normalized;
        cam.transform.position = center + dir * dist;
        cam.transform.LookAt(center, Vector3.up);

        float depth = dist + radius * 2.5f;
        cam.nearClipPlane = Mathf.Max(0.01f, depth * 0.001f);
        cam.farClipPlane = Mathf.Max(50f, depth * 2f);
    }

    public void Close()
    {
        if (_cam != null)
            _cam.targetTexture = null;
        _cam = null;

        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        if (_modal != null)
        {
            Destroy(_modal);
            _modal = null;
        }

        if (_worldRoot != null)
        {
            Destroy(_worldRoot);
            _worldRoot = null;
        }
    }

    void OnDestroy()
    {
        Close();
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    GameObject CreateUiLabel(RectTransform parent, string name, string text, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.GetComponent<RectTransform>().SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = new Color32(40, 44, 52, 255);
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        StretchFull(go.GetComponent<RectTransform>());
        return go;
    }

    static Button CreateTextButton(RectTransform parent, string name, string label, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color32(235, 238, 245, 255);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        textGo.GetComponent<RectTransform>().SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var t = textGo.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color32(40, 44, 52, 255);
        t.text = label;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return btn;
    }
}

/// <summary>
/// 在 RawImage 上拖拽旋转模型枢轴：绕「预览相机」的右轴 / 上轴增量旋转，
/// 等价于屏幕平面内的轨迹球，可达任意朝向（无欧拉俯仰 ±85° 限制）。
/// </summary>
sealed class TutorialPreviewOrbitDrag : MonoBehaviour, IDragHandler
{
    public Transform target;
    public Camera referenceCamera;

    [Tooltip("灵敏度：像素位移 × 该系数 ≈ 旋转角度（度）")]
    public float degreesPerPixel = 0.35f;

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null)
            return;

        float dx = eventData.delta.x;
        float dy = eventData.delta.y;
        float s = degreesPerPixel;

        if (referenceCamera != null)
        {
            var right = referenceCamera.transform.right;
            var up = referenceCamera.transform.up;
            target.Rotate(right, -dy * s, Space.World);
            target.Rotate(up, dx * s, Space.World);
        }
        else
        {
            target.Rotate(Vector3.right, -dy * s, Space.World);
            target.Rotate(Vector3.up, dx * s, Space.World);
        }
    }
}
