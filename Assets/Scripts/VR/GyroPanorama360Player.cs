using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 手机式 360 全景播放：equirectangular 静态图 / 视频贴在内翻球面上，陀螺仪或鼠标拖拽环视。
/// </summary>
[DisallowMultipleComponent]
public class GyroPanorama360Player : MonoBehaviour
{
    const float SphereRadius = 48f;
    const float MaxPitch = 85f;
    const int VideoRtHeight = 2048;
    const float PanoramaCameraDepth = 80f;
    /// <summary>全景球专用层，避免场景里 Background 装饰板挡住环视。</summary>
    const int PanoramaOnlyLayer = 30;

    GameObject _worldRoot;
    Transform _head;
    Camera _camera;
    Material _sphereMaterial;
    RenderTexture _videoRt;
    VideoPlayer _videoPlayer;
    GameObject _sphereGo;

    bool _active;
    bool _gyroReady;
    Quaternion _gyroBase = Quaternion.identity;
    Vector2 _dragLook;
    Coroutine _prepareRoutine;
    int _sleepTimeoutBackup = SleepTimeout.SystemSetting;
    Texture2D _ownedStaticTexture;
    readonly List<Camera> _disabledCameras = new List<Camera>(4);
    readonly List<GameObject> _hiddenDecor = new List<GameObject>(8);

    public bool IsActive => _active;

    public event Action<bool, string> OnPlaybackStateChanged;

    public void Enter()
    {
        if (_active)
            return;

        BuildWorldIfNeeded();
        _worldRoot.SetActive(true);
        if (_camera != null)
            _camera.enabled = true;

        SuspendOtherCameras();
        HideSceneDecor();
        _active = true;
        ResetLook();
        TryEnableGyro();
        _sleepTimeoutBackup = Screen.sleepTimeout;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    public void Exit()
    {
        if (!_active)
            return;

        _active = false;
        StopPlaybackInternal();
        DisableGyro();
        Screen.sleepTimeout = _sleepTimeoutBackup;
        RestoreOtherCameras();
        RestoreSceneDecor();

        if (_camera != null)
            _camera.enabled = false;
        if (_worldRoot != null)
            _worldRoot.SetActive(false);
    }

    /// <summary>加载本页 360 资源；videoPath / imagePath 为绝对路径，可只提供其一。</summary>
    public void SetPageSource(string videoPath, string imagePath)
    {
        BuildWorldIfNeeded();
        StopPlaybackInternal();

        if (!string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath))
        {
            StartPrepareVideo(videoPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            var tex = LoadTextureFromFile(imagePath);
            if (tex != null)
            {
                ApplyStaticPanorama(tex);
                OnPlaybackStateChanged?.Invoke(true, null);
                return;
            }
        }

        OnPlaybackStateChanged?.Invoke(false, "未找到本页 360 资源");
    }

    /// <summary>没有专用全景图时，用平面页生成 2:1 种子环视图（仅演示，非 AI 全景）。</summary>
    public void SetFallbackFromFlatTexture(Texture2D flatPage)
    {
        BuildWorldIfNeeded();
        StopPlaybackInternal();

        if (flatPage == null)
        {
            OnPlaybackStateChanged?.Invoke(false, "本页没有可展示的画面");
            return;
        }

        var owned = new System.Collections.Generic.List<Texture2D>();
        var pano = StoryImageUtil.BuildEquirectangularCover(flatPage, 1024, 512, owned);
        // 只保留最终种子图，中间可读拷贝可销毁
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i] != pano)
                Destroy(owned[i]);
        }

        if (pano == null)
        {
            OnPlaybackStateChanged?.Invoke(false, "无法生成本页临时全景");
            return;
        }

        ApplyStaticPanorama(pano);
        OnPlaybackStateChanged?.Invoke(true, null);
    }

    void Update()
    {
        if (!_active || _head == null)
            return;

        ApplyLookInput();
    }

    void OnDisable() => Exit();

    void OnDestroy()
    {
        Exit();
        StopPlaybackInternal();
        DisableGyro();
        ReleaseOwnedStaticTexture();
        if (_sphereMaterial != null)
            Destroy(_sphereMaterial);
        ReleaseVideoRt();
        if (_worldRoot != null)
            Destroy(_worldRoot);
    }

    void BuildWorldIfNeeded()
    {
        if (_worldRoot != null)
            return;

        _worldRoot = new GameObject("Panorama360World");
        DontDestroyOnLoad(_worldRoot);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "PanoramaSphere";
        sphere.layer = PanoramaOnlyLayer;
        sphere.transform.SetParent(_worldRoot.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * SphereRadius;
        _sphereGo = sphere;
        var col = sphere.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var shader = Shader.Find("StoryBricks/EquirectangularInside")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Sprites/Default");
        _sphereMaterial = new Material(shader);
        sphere.GetComponent<MeshRenderer>().sharedMaterial = _sphereMaterial;

        _head = new GameObject("PanoramaHead").transform;
        _head.SetParent(_worldRoot.transform, false);
        _head.localPosition = Vector3.zero;
        SetLayerRecursively(_head.gameObject, PanoramaOnlyLayer);

        var camGo = new GameObject("PanoramaCamera");
        camGo.layer = PanoramaOnlyLayer;
        camGo.transform.SetParent(_head, false);
        camGo.transform.localPosition = Vector3.zero;
        camGo.transform.localRotation = Quaternion.identity;
        _camera = camGo.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black;
        _camera.fieldOfView = 90f;
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = SphereRadius * 2f;
        // 只看全景球，不看场景里的 Background 装饰板
        _camera.cullingMask = 1 << PanoramaOnlyLayer;
        _camera.depth = PanoramaCameraDepth;
        _camera.enabled = false;

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.skipOnDrop = true;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        _worldRoot.SetActive(false);
    }

    void SuspendOtherCameras()
    {
        RestoreOtherCameras();
        var cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            if (cam == null || cam == _camera || !cam.enabled)
                continue;
            // Overlay UI 不依赖这些相机；关掉以免清屏盖住全景
            cam.enabled = false;
            _disabledCameras.Add(cam);
        }
    }

    void RestoreOtherCameras()
    {
        for (int i = 0; i < _disabledCameras.Count; i++)
        {
            if (_disabledCameras[i] != null)
                _disabledCameras[i].enabled = true;
        }
        _disabledCameras.Clear();
    }

    void HideSceneDecor()
    {
        RestoreSceneDecor();
        // 阅读场景里的 StoryLibraryDecor/Background 是一张大图板，会挡在全景中间
        var renders = FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < renders.Length; i++)
        {
            var sr = renders[i];
            if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy)
                continue;
            if (_worldRoot != null && sr.transform.IsChildOf(_worldRoot.transform))
                continue;
            sr.gameObject.SetActive(false);
            _hiddenDecor.Add(sr.gameObject);
        }

        var decorRoot = GameObject.Find("StoryLibraryDecor");
        if (decorRoot != null && decorRoot.activeSelf)
        {
            decorRoot.SetActive(false);
            if (!_hiddenDecor.Contains(decorRoot))
                _hiddenDecor.Add(decorRoot);
        }
    }

    void RestoreSceneDecor()
    {
        for (int i = 0; i < _hiddenDecor.Count; i++)
        {
            if (_hiddenDecor[i] != null)
                _hiddenDecor[i].SetActive(true);
        }
        _hiddenDecor.Clear();
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    void StartPrepareVideo(string absolutePath)
    {
        if (_prepareRoutine != null)
            StopCoroutine(_prepareRoutine);
        _prepareRoutine = StartCoroutine(PrepareAndPlayVideo(absolutePath));
    }

    IEnumerator PrepareAndPlayVideo(string absolutePath)
    {
        EnsureVideoRt();
        _videoPlayer.targetTexture = _videoRt;
        _sphereMaterial.mainTexture = _videoRt;

        _videoPlayer.Stop();
        _videoPlayer.url = ToFileUrl(absolutePath);

        _videoPlayer.Prepare();
        while (!_videoPlayer.isPrepared)
            yield return null;

        _videoPlayer.Play();
        _prepareRoutine = null;
        OnPlaybackStateChanged?.Invoke(true, null);
    }

    void ApplyStaticPanorama(Texture2D texture)
    {
        ReleaseOwnedStaticTexture();
        _ownedStaticTexture = texture;
        if (_sphereMaterial != null)
            _sphereMaterial.mainTexture = texture;
    }

    void ReleaseOwnedStaticTexture()
    {
        if (_ownedStaticTexture == null)
            return;

        Destroy(_ownedStaticTexture);
        _ownedStaticTexture = null;
    }

    void StopPlaybackInternal()
    {
        if (_prepareRoutine != null)
        {
            StopCoroutine(_prepareRoutine);
            _prepareRoutine = null;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.Stop();
            _videoPlayer.url = null;
        }
    }

    void EnsureVideoRt()
    {
        if (_videoRt != null && _videoRt.height == VideoRtHeight)
            return;

        ReleaseVideoRt();
        int width = VideoRtHeight * 2;
        _videoRt = new RenderTexture(width, VideoRtHeight, 0, RenderTextureFormat.ARGB32)
        {
            wrapMode = TextureWrapMode.Repeat
        };
        _videoRt.Create();
    }

    void ReleaseVideoRt()
    {
        if (_videoRt == null)
            return;

        if (_videoPlayer != null)
            _videoPlayer.targetTexture = null;

        _videoRt.Release();
        Destroy(_videoRt);
        _videoRt = null;
    }

    static Texture2D LoadTextureFromFile(string path)
    {
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Destroy(tex);
                return null;
            }

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GyroPanorama360] 加载全景图失败: {ex.Message}");
            return null;
        }
    }

    static string ToFileUrl(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return "";

        string normalized = Path.GetFullPath(absolutePath).Replace("\\", "/");
        if (normalized.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return normalized;

        return "file:///" + normalized;
    }

    void ResetLook()
    {
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
        if (_gyroReady)
        {
            var gyroRot = GyroToUnity(Input.gyro.attitude);
            _head.localRotation = Quaternion.Inverse(_gyroBase) * gyroRot;
            return;
        }

        // PC：按住左键拖拽；点在 UI 上时不转视角；松开后保持视角
        bool pointerOnUi = UnityEngine.EventSystems.EventSystem.current != null &&
                           UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        if (Input.GetMouseButton(0) && !pointerOnUi)
        {
            _dragLook.x += Input.GetAxis("Mouse X") * 2.2f;
            _dragLook.y -= Input.GetAxis("Mouse Y") * 2.2f;
            _dragLook.y = Mathf.Clamp(_dragLook.y, -MaxPitch, MaxPitch);
        }

        _head.localRotation = Quaternion.Euler(_dragLook.y, _dragLook.x, 0f);
    }

    static Quaternion GyroToUnity(Quaternion q) =>
        new Quaternion(q.x, q.y, -q.z, -q.w);
}
