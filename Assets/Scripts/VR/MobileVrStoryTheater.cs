using UnityEngine;

/// <summary>
/// 轻量移动 VR 剧场：陀螺仪 / 鼠标环视，可选左右眼分屏（Cardboard 式）。
/// 在 3D 空间中固定展示当前故事页。
/// </summary>
[DisallowMultipleComponent]
public class MobileVrStoryTheater : MonoBehaviour
{
    const float PageDistance = 2.8f;
    const float PageHeight = 1.6f;
    const float EyeSeparation = 0.064f;
    const float MaxPitch = 72f;

    GameObject _worldRoot;
    Transform _head;
    Camera _monoCamera;
    Camera _leftCamera;
    Camera _rightCamera;

    Transform _pageRoot;
    MeshRenderer _pageRenderer;
    Material _pageMaterial;
    TextMesh _captionText;

    bool _active;
    bool _stereo;
    bool _gyroReady;
    Quaternion _gyroBase = Quaternion.identity;
    float _yaw;
    float _pitch;
    Vector2 _dragLook;

    public bool IsActive => _active;
    public bool StereoEnabled => _stereo;

    public void Enter()
    {
        if (_active)
            return;

        BuildWorldIfNeeded();
        _worldRoot.SetActive(true);
        _active = true;
        ResetLook();
        TryEnableGyro();
        ApplyCameraLayout();
    }

    public void Exit()
    {
        if (!_active)
            return;

        _active = false;
        DisableGyro();
        if (_worldRoot != null)
            _worldRoot.SetActive(false);
    }

    public void SetStereoEnabled(bool enabled)
    {
        _stereo = enabled;
        if (_active)
            ApplyCameraLayout();
    }

    public void SetPage(Sprite sprite, string caption)
    {
        if (!_active && _worldRoot == null)
            return;

        BuildWorldIfNeeded();
        ApplySpriteToPage(sprite);
        if (_captionText != null)
        {
            _captionText.text = caption ?? "";
            _captionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(caption));
        }
    }

    void Update()
    {
        if (!_active || _head == null)
            return;

        ApplyLookInput();
    }

    void OnDisable()
    {
        Exit();
    }

    void OnDestroy()
    {
        DisableGyro();
        if (_pageMaterial != null)
            Destroy(_pageMaterial);
    }

    void BuildWorldIfNeeded()
    {
        if (_worldRoot != null)
            return;

        _worldRoot = new GameObject("MobileVrStoryWorld");
        _worldRoot.transform.SetParent(null, false);

        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "TheaterDome";
        dome.transform.SetParent(_worldRoot.transform, false);
        dome.transform.localScale = Vector3.one * 40f;
        var domeCol = dome.GetComponent<Collider>();
        if (domeCol != null)
            Destroy(domeCol);
        var domeRenderer = dome.GetComponent<MeshRenderer>();
        domeRenderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"))
        {
            color = new Color32(18, 22, 34, 255)
        };
        dome.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "TheaterFloor";
        floor.transform.SetParent(_worldRoot.transform, false);
        floor.transform.localScale = new Vector3(8f, 0.02f, 8f);
        floor.transform.localPosition = new Vector3(0f, 0f, PageDistance * 0.35f);
        var floorCol = floor.GetComponent<Collider>();
        if (floorCol != null)
            Destroy(floorCol);
        var floorRenderer = floor.GetComponent<MeshRenderer>();
        floorRenderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"))
        {
            color = new Color32(28, 32, 44, 255)
        };

        _head = new GameObject("VrHead").transform;
        _head.SetParent(_worldRoot.transform, false);
        _head.localPosition = new Vector3(0f, 1.45f, 0f);

        _monoCamera = CreateEyeCamera("VrMonoCamera", _head, 0f);
        _leftCamera = CreateEyeCamera("VrLeftCamera", _head, -EyeSeparation * 0.5f);
        _rightCamera = CreateEyeCamera("VrRightCamera", _head, EyeSeparation * 0.5f);

        _pageRoot = new GameObject("StoryPage").transform;
        _pageRoot.SetParent(_worldRoot.transform, false);
        _pageRoot.localPosition = new Vector3(0f, 1.45f, PageDistance);
        _pageRoot.localRotation = Quaternion.identity;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "PageQuad";
        quad.transform.SetParent(_pageRoot, false);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = Quaternion.identity;
        var pageCol = quad.GetComponent<Collider>();
        if (pageCol != null)
            Destroy(pageCol);

        _pageRenderer = quad.GetComponent<MeshRenderer>();
        _pageMaterial = new Material(Shader.Find("Unlit/Texture"));
        _pageRenderer.sharedMaterial = _pageMaterial;
        quad.transform.localScale = new Vector3(PageHeight * 1.4f, PageHeight, 1f);

        var captionGo = new GameObject("Caption");
        captionGo.transform.SetParent(_pageRoot, false);
        captionGo.transform.localPosition = new Vector3(0f, -PageHeight * 0.62f, -0.02f);
        captionGo.transform.localRotation = Quaternion.identity;
        captionGo.transform.localScale = Vector3.one * 0.02f;
        _captionText = captionGo.AddComponent<TextMesh>();
        _captionText.fontSize = 48;
        _captionText.characterSize = 1f;
        _captionText.anchor = TextAnchor.UpperCenter;
        _captionText.alignment = TextAlignment.Center;
        _captionText.color = Color.white;
        _captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _worldRoot.SetActive(false);
    }

    static Camera CreateEyeCamera(string name, Transform parent, float localX)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localX, 0f, 0f);
        go.transform.localRotation = Quaternion.identity;

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(18, 22, 34, 255);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        int uiLayer = LayerMask.NameToLayer("UI");
        cam.cullingMask = uiLayer >= 0 ? ~(1 << uiLayer) : ~0;
        cam.enabled = false;
        return cam;
    }

    void ApplyCameraLayout()
    {
        if (_monoCamera == null)
            return;

        _monoCamera.enabled = !_stereo;
        _leftCamera.enabled = _stereo;
        _rightCamera.enabled = _stereo;

        if (_stereo)
        {
            _leftCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
            _rightCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
        }
        else
        {
            _monoCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    void ApplySpriteToPage(Sprite sprite)
    {
        if (_pageMaterial == null || _pageRoot == null)
            return;

        if (sprite == null || sprite.texture == null)
        {
            _pageMaterial.mainTexture = Texture2D.whiteTexture;
            _pageMaterial.mainTextureScale = Vector2.one;
            _pageMaterial.mainTextureOffset = Vector2.zero;
            return;
        }

        var tex = sprite.texture;
        var rect = sprite.textureRect;
        _pageMaterial.mainTexture = tex;
        _pageMaterial.mainTextureScale = new Vector2(
            rect.width / tex.width,
            rect.height / tex.height);
        _pageMaterial.mainTextureOffset = new Vector2(
            rect.x / tex.width,
            rect.y / tex.height);

        float aspect = rect.width / Mathf.Max(1f, rect.height);
        _pageRoot.localScale = Vector3.one;
        var quad = _pageRenderer.transform;
        quad.localScale = new Vector3(PageHeight * aspect, PageHeight, 1f);
    }

    void ResetLook()
    {
        _yaw = 0f;
        _pitch = 0f;
        _dragLook = Vector2.zero;
        if (_head != null)
            _head.localRotation = Quaternion.identity;
        CaptureGyroBase();
    }

    void TryEnableGyro()
    {
        if (!SystemInfo.supportsGyroscope)
            return;

        Input.gyro.enabled = true;
        _gyroReady = Input.gyro.enabled;
        CaptureGyroBase();
    }

    void DisableGyro()
    {
        if (SystemInfo.supportsGyroscope)
            Input.gyro.enabled = false;
        _gyroReady = false;
    }

    void CaptureGyroBase()
    {
        if (!_gyroReady)
            return;
        _gyroBase = GyroToUnity(Input.gyro.attitude);
    }

    void ApplyLookInput()
    {
        Quaternion look = Quaternion.identity;

        if (_gyroReady)
        {
            var gyroRot = GyroToUnity(Input.gyro.attitude);
            look = Quaternion.Inverse(_gyroBase) * gyroRot;
        }
        else if (Input.GetMouseButton(0))
        {
            _dragLook.x += Input.GetAxis("Mouse X") * 1.6f;
            _dragLook.y -= Input.GetAxis("Mouse Y") * 1.6f;
            _dragLook.y = Mathf.Clamp(_dragLook.y, -MaxPitch, MaxPitch);
            look = Quaternion.Euler(_dragLook.y, _dragLook.x, 0f);
        }
        else
        {
            look = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        _head.localRotation = look;
    }

    static Quaternion GyroToUnity(Quaternion q)
    {
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }
}
