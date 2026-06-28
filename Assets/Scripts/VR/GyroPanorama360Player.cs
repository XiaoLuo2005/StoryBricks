using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 手机式 360 全景播放：视频或 equirectangular 静态图贴在内翻球面上，陀螺仪 / 鼠标拖拽环视。
/// </summary>
[DisallowMultipleComponent]
public class GyroPanorama360Player : MonoBehaviour
{
    const float SphereRadius = 48f;
    const float MaxPitch = 85f;
    const int VideoRtHeight = 2048;

    GameObject _worldRoot;
    Transform _head;
    Camera _camera;
    Material _sphereMaterial;
    RenderTexture _videoRt;
    VideoPlayer _videoPlayer;

    bool _active;
    bool _gyroReady;
    Quaternion _gyroBase = Quaternion.identity;
    Vector2 _dragLook;
    Coroutine _prepareRoutine;
    int _sleepTimeoutBackup = SleepTimeout.SystemSetting;
    Texture2D _ownedStaticTexture;

    public bool IsActive => _active;

    public event Action<bool, string> OnPlaybackStateChanged;

    public void Enter()
    {
        if (_active)
            return;

        BuildWorldIfNeeded();
        _worldRoot.SetActive(true);
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
        if (_worldRoot != null)
            _worldRoot.SetActive(false);
    }

    /// <summary>加载本页 360 资源；videoPath / imagePath 为绝对路径，可只提供其一。</summary>
    public void SetPageSource(string videoPath, string imagePath)
    {
        if (!_active && _worldRoot == null)
            return;

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

    void Update()
    {
        if (!_active || _head == null)
            return;

        ApplyLookInput();
    }

    void OnDisable() => Exit();

    void OnDestroy()
    {
        StopPlaybackInternal();
        DisableGyro();
        ReleaseOwnedStaticTexture();
        if (_sphereMaterial != null)
            Destroy(_sphereMaterial);
        ReleaseVideoRt();
    }

    void BuildWorldIfNeeded()
    {
        if (_worldRoot != null)
            return;

        _worldRoot = new GameObject("Panorama360World");
        _worldRoot.transform.SetParent(null, false);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "PanoramaSphere";
        sphere.transform.SetParent(_worldRoot.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = new Vector3(-SphereRadius, SphereRadius, SphereRadius);
        var col = sphere.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        _sphereMaterial = new Material(Shader.Find("Unlit/Texture"));
        sphere.GetComponent<MeshRenderer>().sharedMaterial = _sphereMaterial;

        _head = new GameObject("PanoramaHead").transform;
        _head.SetParent(_worldRoot.transform, false);
        _head.localPosition = Vector3.zero;

        var camGo = new GameObject("PanoramaCamera");
        camGo.transform.SetParent(_head, false);
        camGo.transform.localPosition = Vector3.zero;
        camGo.transform.localRotation = Quaternion.identity;
        _camera = camGo.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black;
        _camera.fieldOfView = 90f;
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = SphereRadius * 2f;
        int uiLayer = LayerMask.NameToLayer("UI");
        _camera.cullingMask = uiLayer >= 0 ? ~(1 << uiLayer) : ~0;
        _camera.depth = -10;

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.skipOnDrop = true;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        _worldRoot.SetActive(false);
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
        Quaternion look;
        if (_gyroReady)
        {
            var gyroRot = GyroToUnity(Input.gyro.attitude);
            look = Quaternion.Inverse(_gyroBase) * gyroRot;
        }
        else if (Input.GetMouseButton(0))
        {
            _dragLook.x += Input.GetAxis("Mouse X") * 1.8f;
            _dragLook.y -= Input.GetAxis("Mouse Y") * 1.8f;
            _dragLook.y = Mathf.Clamp(_dragLook.y, -MaxPitch, MaxPitch);
            look = Quaternion.Euler(_dragLook.y, _dragLook.x, 0f);
        }
        else
        {
            look = Quaternion.identity;
        }

        _head.localRotation = look;
    }

    static Quaternion GyroToUnity(Quaternion q) =>
        new Quaternion(q.x, q.y, -q.z, -q.w);
}
