using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 连续麦克风监听：检测到说话后，在短暂停顿时自动切出一段 WAV（模仿自然对话，无需按住说话）。
/// </summary>
[DisallowMultipleComponent]
public class ContinuousVoiceListener : MonoBehaviour
{
    const int DefaultSampleRate = 16000;
    const int LoopClipSeconds = 30;

    [SerializeField] float speechThreshold = 0.009f;
    [SerializeField] float speechStartHoldSeconds = 0.12f;
    [SerializeField] float silenceEndSeconds = 1.2f;
    [SerializeField] float minUtteranceSeconds = 0.35f;
    [SerializeField] float maxUtteranceSeconds = 14f;
    [SerializeField] float cooldownSeconds = 0.45f;
    [Tooltip("留空则按优先级自动选择；填 Realtek / PicoStreamingMicrophone 等关键字即可匹配")]
    [SerializeField] string preferredMicDeviceName = "Realtek";

    readonly List<float> _utterance = new List<float>(DefaultSampleRate * 8);
    readonly Queue<float> _levelWindow = new Queue<float>(64);

    string _micDevice;
    AudioClip _micClip;
    int _lastMicPos;
    int _micWarmupFrames;
    bool _loggedMicNotReady;
    bool _active;
    bool _paused;
    bool _inSpeech;
    bool _armed;
    float _speechHold;
    float _silence;
    float _cooldown;
    float _utteranceSeconds;

    Action<byte[]> _onUtterance;
    Action<string> _onError;
    Action<bool> _onSpeakingChanged;

    public bool IsActive => _active;
    public bool IsPaused => _paused;
    public bool IsSpeaking => _active && !_paused && _inSpeech;
    public string ActiveMicDevice => _micDevice ?? "";

    public bool StartListening(
        Action<byte[]> onUtterance,
        Action<string> onError = null,
        Action<bool> onSpeakingChanged = null)
    {
        StopListening();
        if (!EnsureMicPermission())
        {
            onError?.Invoke("需要麦克风权限");
            return false;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            onError?.Invoke("未检测到麦克风");
            return false;
        }

        for (int i = 0; i < Microphone.devices.Length; i++)
            Debug.Log($"[ContinuousVoiceListener] 可用麦克风 {i}: {Microphone.devices[i]}");

        _onUtterance = onUtterance;
        _onError = onError;
        _onSpeakingChanged = onSpeakingChanged;
        _micDevice = PickMicDevice(preferredMicDeviceName);
        if (string.IsNullOrEmpty(_micDevice))
        {
            onError?.Invoke("未检测到麦克风");
            return false;
        }

        Debug.Log($"[ContinuousVoiceListener] 使用麦克风: {_micDevice}");
        _micClip = Microphone.Start(_micDevice, true, LoopClipSeconds, DefaultSampleRate);
        _lastMicPos = 0;
        _micWarmupFrames = 30;
        _loggedMicNotReady = false;
        _active = true;
        _paused = false;
        ResetSpeechState();
        return true;
    }

    public void StopListening()
    {
        _active = false;
        _paused = false;
        _onUtterance = null;
        _onError = null;
        _onSpeakingChanged = null;
        EndMic();
        ResetSpeechState();
    }

    public void Pause()
    {
        if (!_active)
            return;
        _paused = true;
        ResetSpeechState();
        _onSpeakingChanged?.Invoke(false);
    }

    public void Resume()
    {
        if (!_active)
            return;
        _paused = false;
        _cooldown = cooldownSeconds;
        ResetSpeechState();
    }

    void Update()
    {
        if (!_active || _paused || _micClip == null)
            return;

        if (_cooldown > 0f)
        {
            _cooldown -= Time.unscaledDeltaTime;
            return;
        }

        PumpMicSamples();
    }

    void OnDisable() => StopListening();

    void PumpMicSamples()
    {
        if (_micWarmupFrames > 0)
        {
            _micWarmupFrames--;
            return;
        }

        int pos = Microphone.GetPosition(_micDevice);
        if (pos < 0)
        {
            if (!_loggedMicNotReady)
            {
                Debug.LogWarning($"[ContinuousVoiceListener] 麦克风尚未就绪: {_micDevice}");
                _loggedMicNotReady = true;
            }
            return;
        }

        _loggedMicNotReady = false;

        int total = _micClip.samples;
        if (pos == _lastMicPos)
            return;

        if (pos > _lastMicPos)
            AppendMicRange(_lastMicPos, pos);
        else
        {
            AppendMicRange(_lastMicPos, total);
            AppendMicRange(0, pos);
        }

        _lastMicPos = pos;
    }

    void AppendMicRange(int startSample, int endSample)
    {
        int sampleCount = endSample - startSample;
        if (sampleCount <= 0)
            return;

        int channels = _micClip.channels;
        var interleaved = new float[sampleCount * channels];
        _micClip.GetData(interleaved, startSample);

        if (channels > 1)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += interleaved[i * channels + c];
                ProcessSample(sum / channels);
            }
        }
        else
        {
            for (int i = 0; i < sampleCount; i++)
                ProcessSample(interleaved[i]);
        }
    }

    void ProcessSample(float sample)
    {
        float dt = 1f / DefaultSampleRate;
        float level = Mathf.Abs(sample);
        PushLevel(level);
        float rms = ComputeRms();

        if (!_inSpeech)
        {
            if (rms >= speechThreshold)
            {
                _speechHold += dt;
                if (_speechHold >= speechStartHoldSeconds)
                    BeginSpeech();
            }
            else
            {
                _speechHold = 0f;
            }

            return;
        }

        _utterance.Add(sample);
        _utteranceSeconds += dt;

        if (rms >= speechThreshold * 0.72f)
            _silence = 0f;
        else
            _silence += dt;

        if (_utteranceSeconds >= maxUtteranceSeconds)
        {
            CompleteUtterance();
            return;
        }

        if (_silence >= silenceEndSeconds && _utteranceSeconds >= minUtteranceSeconds)
            CompleteUtterance();
    }

    void BeginSpeech()
    {
        _inSpeech = true;
        _armed = true;
        _utterance.Clear();
        _utteranceSeconds = 0f;
        _silence = 0f;
        _onSpeakingChanged?.Invoke(true);
    }

    void CompleteUtterance()
    {
        if (!_armed || _utterance.Count == 0)
        {
            ResetSpeechState();
            return;
        }

        var wav = PcmFloatWavEncoder.EncodeMono16(_utterance.ToArray(), DefaultSampleRate);
        ResetSpeechState();
        _cooldown = cooldownSeconds;
        _onUtterance?.Invoke(wav);
    }

    void ResetSpeechState()
    {
        bool wasSpeaking = _inSpeech;
        _inSpeech = false;
        _armed = false;
        _speechHold = 0f;
        _silence = 0f;
        _utteranceSeconds = 0f;
        _utterance.Clear();
        _levelWindow.Clear();
        if (wasSpeaking)
            _onSpeakingChanged?.Invoke(false);
    }

    void PushLevel(float level)
    {
        _levelWindow.Enqueue(level * level);
        while (_levelWindow.Count > 64)
            _levelWindow.Dequeue();
    }

    float ComputeRms()
    {
        if (_levelWindow.Count == 0)
            return 0f;
        float sum = 0f;
        foreach (float v in _levelWindow)
            sum += v;
        return Mathf.Sqrt(sum / _levelWindow.Count);
    }

    void EndMic()
    {
        if (!string.IsNullOrEmpty(_micDevice) && Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);
        _micDevice = null;
        if (_micClip != null)
        {
            Destroy(_micClip);
            _micClip = null;
        }
        _lastMicPos = 0;
    }

    static bool EnsureMicPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
        }
#endif
#if UNITY_IOS && !UNITY_EDITOR
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
        return Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
        return true;
    }

    static string PickMicDevice(string preferredName)
    {
        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            var want = preferredName.Trim();
            foreach (var device in devices)
            {
                if (string.Equals(device, want, StringComparison.OrdinalIgnoreCase))
                    return device;
            }

            foreach (var device in devices)
            {
                if (device.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                    return device;
            }
        }

        string best = null;
        int bestScore = int.MinValue;
        foreach (var device in devices)
        {
            int score = ScoreMicDevice(device);
            if (score < 0)
                continue;
            if (score > bestScore)
            {
                bestScore = score;
                best = device;
            }
        }

        if (best != null)
            return best;

        return devices[0];
    }

    static int ScoreMicDevice(string device)
    {
        var lower = device.ToLowerInvariant();

        if (lower.Contains("ivcam") ||
            lower.Contains("virtual") ||
            lower.Contains("cable") ||
            lower.Contains("loopback") ||
            lower.Contains("stereo mix") ||
            lower.Contains("立体声混音") ||
            lower.Contains("what u hear"))
            return -1;

        // Pico / VR 会把扬声器也注册成「麦克风」，需排除
        if (lower.Contains("streaming speaker") || lower.Contains("pico streaming speaker"))
            return -1;
        if (lower.Contains("speaker") &&
            !lower.Contains("microphone") &&
            !lower.Contains("streamingmicrophone") &&
            !lower.Contains("麦克风"))
            return -1;

        int score = 0;
        if (lower.Contains("realtek"))
            score += 120;
        if (lower.Contains("streamingmicrophone") || lower.Contains("pico streaming microphone"))
            score += 100;
        if (lower.Contains("microphone") || lower.Contains("麦克风"))
            score += 40;
        if (lower.Contains("array") || lower.Contains("阵列"))
            score += 10;

        return score;
    }
}
