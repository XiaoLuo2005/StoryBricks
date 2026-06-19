using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 故事创作语音网关客户端：TTS 提问、ASR 收录儿童回答。需运行 storybricks-tutor-gateway（默认 8787）。
/// </summary>
public class StoryCreationVoiceGateway : MonoBehaviour
{
    [SerializeField] string gatewayBaseUrl = "http://127.0.0.1:8787";

    const int MicSampleRate = 16000;
    const int MicMaxSeconds = 12;

    AudioSource _audio;
    string _micDevice;
    AudioClip _micClip;
    bool _micRecording;
    UnityWebRequest _active;

    public string GatewayBaseUrl
    {
        get => gatewayBaseUrl;
        set => gatewayBaseUrl = (value ?? "").Trim().TrimEnd('/');
    }

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
    }

    void OnDestroy() => CancelRequest();
    void OnDisable()
    {
        CancelRequest();
        StopMicIfAny();
    }

    public void CancelRequest()
    {
        if (_active != null)
        {
            _active.Abort();
            _active.Dispose();
            _active = null;
        }
    }

    public void StopPlayback()
    {
        if (_audio != null && _audio.isPlaying)
            _audio.Stop();
    }

    public IEnumerator SpeakText(string text, Action<bool, string> onDone = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onDone?.Invoke(false, "提问为空");
            yield break;
        }

        StopPlayback();
        CancelRequest();
        var body = JsonUtility.ToJson(new TtsRequest { text = text.Trim() });
        var url = $"{GatewayBaseUrl}/api/story-creation/tts";
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            var err = string.IsNullOrEmpty(req.error) ? "TTS 网络错误" : req.error;
            Debug.LogWarning($"[StoryCreationVoice] {err}");
            onDone?.Invoke(false, err);
            yield break;
        }

        var resp = JsonUtility.FromJson<TtsResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error) || string.IsNullOrEmpty(resp.audioBase64))
        {
            var err = resp?.error ?? "TTS 无音频";
            Debug.LogWarning($"[StoryCreationVoice] TTS 失败: {err}");
            onDone?.Invoke(false, err);
            yield break;
        }

        yield return PlayAudioBase64(resp.audioBase64, resp.audioFormat);
        if (_audio.isPlaying)
            yield return new WaitWhile(() => _audio.isPlaying);
        onDone?.Invoke(true, "");
    }

    public IEnumerator TranscribeWav(byte[] wavBytes, AsrContext context, Action<string, string> onDone)
    {
        if (wavBytes == null || wavBytes.Length == 0)
        {
            onDone?.Invoke("", "录音为空");
            yield break;
        }

        CancelRequest();
        var form = new WWWForm();
        form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav");
        if (context != null)
        {
            if (!string.IsNullOrWhiteSpace(context.gapKind))
                form.AddField("gapKind", context.gapKind);
            if (!string.IsNullOrWhiteSpace(context.roleName))
                form.AddField("roleName", context.roleName);
        }

        var url = $"{GatewayBaseUrl}/api/story-creation/asr";
        using var req = UnityWebRequest.Post(url, form);
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", req.error);
            yield break;
        }

        var resp = JsonUtility.FromJson<AsrResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "识别失败");
            yield break;
        }

        onDone?.Invoke(resp.transcript?.Trim() ?? "", "");
    }

    public IEnumerator TranscribeWav(byte[] wavBytes, Action<string, string> onDone)
    {
        yield return TranscribeWav(wavBytes, null, onDone);
    }

    public IEnumerator FetchQuestions(
        StoryCreationQuestionsRequest request,
        Action<List<StoryCreationQuestion>, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request);
        var url = $"{GatewayBaseUrl}/api/story-creation/questions";
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(null, req.error);
            yield break;
        }

        var resp = JsonUtility.FromJson<QuestionsResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke(null, resp?.error ?? "提问生成失败");
            yield break;
        }

        var list = new List<StoryCreationQuestion>();
        if (resp.questions != null)
        {
            foreach (var q in resp.questions)
            {
                if (q != null && !string.IsNullOrWhiteSpace(q.text))
                    list.Add(q);
            }
        }
        onDone?.Invoke(list, "");
    }

    public IEnumerator RefineImagePrompt(
        StoryCreationPromptRefineRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationPromptRefineRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/refine-prompt";
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", req.error);
            yield break;
        }

        var resp = JsonUtility.FromJson<PromptRefineResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "Prompt 整理失败");
            yield break;
        }

        onDone?.Invoke(resp.prompt?.Trim() ?? "", "");
    }

    public bool BeginRecording()
    {
        if (_micRecording)
            return false;
        if (!EnsureMicPermission())
            return false;
        if (Microphone.devices == null || Microphone.devices.Length == 0)
            return false;

        StopPlayback();
        _micDevice = Microphone.devices[0];
        _micClip = Microphone.Start(_micDevice, false, MicMaxSeconds, MicSampleRate);
        _micRecording = true;
        return true;
    }

    public bool EndRecordingAndEncode(out byte[] wavBytes, out string error)
    {
        wavBytes = null;
        error = "";
        if (!_micRecording)
        {
            error = "未在录音";
            return false;
        }

        _micRecording = false;
        if (_micClip == null || string.IsNullOrEmpty(_micDevice))
        {
            error = "录音失败";
            return false;
        }

        int pos = Microphone.GetPosition(_micDevice);
        int channels = _micClip.channels;
        Microphone.End(_micDevice);

        if (pos <= 0 || pos > _micClip.samples)
            pos = _micClip.samples;

        var data = new float[pos * channels];
        _micClip.GetData(data, 0);
        Destroy(_micClip);
        _micClip = null;

        if (pos < MicSampleRate / 4)
        {
            error = "太短了，请再试一次";
            return false;
        }

        var mono = channels > 1 ? DownmixToMono(data, channels) : data;
        wavBytes = PcmFloatWavEncoder.EncodeMono16(mono, MicSampleRate);
        return true;
    }

    public void StopMicIfAny()
    {
        if (_micRecording && !string.IsNullOrEmpty(_micDevice))
            Microphone.End(_micDevice);
        _micRecording = false;
        if (_micClip != null)
        {
            Destroy(_micClip);
            _micClip = null;
        }
    }

    IEnumerator PlayAudioBase64(string b64, string format)
    {
        if (string.Equals(format, "mp3", StringComparison.OrdinalIgnoreCase))
        {
            yield return PlayMp3Base64(b64);
            yield break;
        }
        yield return PlayWavBase64(b64);
    }

    IEnumerator PlayMp3Base64(string b64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(b64);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StoryCreationVoice] MP3 解码失败: {e.Message}");
            yield break;
        }

        var path = Path.Combine(Application.temporaryCachePath, $"story_tts_{DateTime.UtcNow.Ticks}.mp3");
        try
        {
            File.WriteAllBytes(path, bytes);
            using var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path.Replace("\\", "/"), AudioType.MPEG);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[StoryCreationVoice] MP3 播放失败: {req.error}");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
                yield break;
            _audio.Stop();
            _audio.clip = clip;
            _audio.Play();
            yield return new WaitWhile(() => _audio.isPlaying);
            Destroy(clip);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    IEnumerator PlayWavBase64(string b64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(b64);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StoryCreationVoice] 音频解码失败: {e.Message}");
            yield break;
        }

        if (bytes.Length < 44)
            yield break;

        int channels = BitConverter.ToInt16(bytes, 22);
        int sampleRate = BitConverter.ToInt32(bytes, 24);
        int bits = BitConverter.ToInt16(bytes, 34);
        if (channels <= 0 || sampleRate <= 0 || bits != 16)
            yield break;

        int dataStart = 44;
        int sampleCount = (bytes.Length - dataStart) / (channels * 2);
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short s = BitConverter.ToInt16(bytes, dataStart + i * channels * 2);
            samples[i] = s / 32768f;
        }

        var clip = AudioClip.Create("story_voice_tts", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        _audio.Stop();
        _audio.clip = clip;
        _audio.Play();
        yield return new WaitWhile(() => _audio.isPlaying);
        Destroy(clip);
    }

    static float[] DownmixToMono(float[] interleaved, int channels)
    {
        int frames = interleaved.Length / channels;
        var m = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float s = 0f;
            for (int c = 0; c < channels; c++)
                s += interleaved[i * channels + c];
            m[i] = s / channels;
        }
        return m;
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

    [Serializable]
    class TtsRequest
    {
        public string text;
    }

    [Serializable]
    class TtsResponse
    {
        public string audioBase64;
        public string audioFormat;
        public string error;
    }

    [Serializable]
    public class AsrContext
    {
        public string gapKind;
        public string roleName;
    }

    [Serializable]
    class AsrResponse
    {
        public string transcript;
        public string error;
    }

    [Serializable]
    public class StoryCreationQuestionsRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string sceneGuideText;
        public string previousSummary;
        public StoryCreationGapDto[] gaps;
    }

    [Serializable]
    public class StoryCreationGapDto
    {
        public string kind;
        public string roleName;
        public string fallbackQuestion;
    }

    [Serializable]
    public class StoryCreationQuestion
    {
        public string id;
        public string text;
    }

    [Serializable]
    class QuestionsResponse
    {
        public StoryCreationQuestion[] questions;
        public string error;
    }

    [Serializable]
    public class StoryCreationPromptRefineRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string stylePromptPrefix;
        public string sceneGuideText;
        public string previousSummary;
        public string voiceSupplement;
        public string detectedRolesDescription;
        public string referenceImageClause;
        public bool isContinuationPage;
    }

    [Serializable]
    class PromptRefineResponse
    {
        public string prompt;
        public string error;
    }
}
