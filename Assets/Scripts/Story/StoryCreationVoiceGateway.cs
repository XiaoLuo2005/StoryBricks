using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 故事创作语音助手「乐乐」：TTS 提问、ASR 收录儿童回答。需运行 storybricks-tutor-gateway（默认 8787）。
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
    ContinuousVoiceListener _continuousListener;

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

        _continuousListener = GetComponent<ContinuousVoiceListener>();
        if (_continuousListener == null)
            _continuousListener = gameObject.AddComponent<ContinuousVoiceListener>();
    }

    void OnDestroy() => CancelRequest();
    void OnDisable()
    {
        CancelRequest();
        StopMicIfAny();
        StopAnswerListening();
    }

    public bool StartAnswerListening(
        Action<byte[]> onUtterance,
        Action<string> onError = null,
        Action<bool> onSpeakingChanged = null)
    {
        StopMicIfAny();
        if (_continuousListener == null)
            return false;
        return _continuousListener.StartListening(onUtterance, onError, onSpeakingChanged);
    }

    public void StopAnswerListening()
    {
        _continuousListener?.StopListening();
    }

    public void PauseAnswerListening()
    {
        _continuousListener?.Pause();
    }

    public void ResumeAnswerListening()
    {
        _continuousListener?.Resume();
    }

    public bool IsAnswerListening => _continuousListener != null && _continuousListener.IsActive;
    public bool IsChildSpeaking => _continuousListener != null && _continuousListener.IsSpeaking;

    public void CancelRequest()
    {
        if (_active != null)
        {
            _active.Abort();
            _active.Dispose();
            _active = null;
        }
    }

    /// <summary>
    /// After SendWebRequest, read result and dispose. Returns false if CancelRequest/OnDisable
    /// already disposed this request — do not touch req in that case.
    /// </summary>
    bool TryCompleteActiveRequest(
        UnityWebRequest req,
        out UnityWebRequest.Result result,
        out string error,
        out string responseText)
    {
        result = UnityWebRequest.Result.ConnectionError;
        error = "";
        responseText = "";

        if (req == null || _active != req)
            return false;

        _active = null;
        try
        {
            result = req.result;
            error = req.error ?? "";
            responseText = req.downloadHandler?.text ?? "";
            return true;
        }
        finally
        {
            req.Dispose();
        }
    }

    public void StopPlayback()
    {
        if (_audio != null && _audio.isPlaying)
            _audio.Stop();
    }

    bool ShouldResumeListeningAfterSpeak()
    {
        return _continuousListener != null &&
               _continuousListener.IsActive &&
               !_continuousListener.IsPaused;
    }

    void ResumeListeningIfNeeded(bool resume)
    {
        if (resume)
            ResumeAnswerListening();
    }

    public IEnumerator SpeakText(string text, Action<bool, string> onDone = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onDone?.Invoke(false, "提问为空");
            yield break;
        }

        StopPlayback();
        bool resumeListeningAfter = ShouldResumeListeningAfterSpeak();
        if (resumeListeningAfter)
            PauseAnswerListening();
        CancelRequest();
        var body = JsonUtility.ToJson(new TtsRequest { text = text.Trim() });
        var url = $"{GatewayBaseUrl}/api/story-creation/tts";
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            ResumeListeningIfNeeded(resumeListeningAfter);
            onDone?.Invoke(false, "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            var err = string.IsNullOrEmpty(reqError) ? "TTS 网络错误" : reqError;
            Debug.LogWarning($"[StoryCreationVoice] {err}");
            ResumeListeningIfNeeded(resumeListeningAfter);
            onDone?.Invoke(false, err);
            yield break;
        }

        var resp = JsonUtility.FromJson<TtsResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error) || string.IsNullOrEmpty(resp.audioBase64))
        {
            var err = resp?.error ?? "TTS 无音频";
            Debug.LogWarning($"[StoryCreationVoice] TTS 失败: {err}");
            ResumeListeningIfNeeded(resumeListeningAfter);
            onDone?.Invoke(false, err);
            yield break;
        }

        yield return PlayAudioBase64(resp.audioBase64, resp.audioFormat);
        if (_audio != null && _audio.isPlaying)
            yield return new WaitWhile(() => _audio != null && _audio.isPlaying);
        ResumeListeningIfNeeded(resumeListeningAfter);
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
        var req = UnityWebRequest.Post(url, form);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<AsrResponse>(responseText);
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
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke(null, "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(null, reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<QuestionsResponse>(responseText);
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
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<PromptRefineResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "Prompt 整理失败");
            yield break;
        }

        onDone?.Invoke(resp.prompt?.Trim() ?? "", "");
    }

    public IEnumerator FetchReply(
        StoryCreationReplyRequest request,
        Action<StoryCreationReplyResult, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationReplyRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/reply";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke(null, "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(null, reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<StoryCreationReplyResult>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke(null, resp?.error ?? "接话失败");
            yield break;
        }
        onDone?.Invoke(resp, "");
    }

    public IEnumerator FetchPageSummary(
        StoryCreationSummaryRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationSummaryRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/summary";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<SummaryResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "摘要失败");
            yield break;
        }
        onDone?.Invoke(resp.summary?.Trim() ?? "", "");
    }

    public IEnumerator FetchPageCaption(
        StoryCreationPageCaptionRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationPageCaptionRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/page-caption";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<PageCaptionResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "旁白生成失败");
            yield break;
        }
        onDone?.Invoke(resp.caption?.Trim() ?? "", "");
    }

    public IEnumerator FetchFreeChatReply(
        StoryCreationFreeChatRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationFreeChatRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/free-chat";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<FreeChatResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "对话失败");
            yield break;
        }
        onDone?.Invoke(resp.reply?.Trim() ?? "", "");
    }

    public IEnumerator FetchWaitNarration(
        StoryCreationNarrationRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationNarrationRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/wait-narration";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<NarrationResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "旁白失败");
            yield break;
        }
        onDone?.Invoke(resp.narration?.Trim() ?? "", "");
    }

    public IEnumerator FetchPageRecap(
        StoryCreationRecapRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationRecapRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/page-recap";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<RecapResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "小结失败");
            yield break;
        }
        onDone?.Invoke(resp.recap?.Trim() ?? "", "");
    }

    public IEnumerator FetchBranchHint(
        StoryCreationBranchRequest request,
        Action<string, string> onDone)
    {
        CancelRequest();
        var json = JsonUtility.ToJson(request ?? new StoryCreationBranchRequest());
        var url = $"{GatewayBaseUrl}/api/story-creation/branch-hint";
        var req = PostJson(url, json);
        _active = req;
        yield return req.SendWebRequest();

        if (!TryCompleteActiveRequest(req, out var result, out var reqError, out var responseText))
        {
            onDone?.Invoke("", "已取消");
            yield break;
        }

        if (result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", reqError);
            yield break;
        }

        var resp = JsonUtility.FromJson<BranchHintResponse>(responseText);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "分支提示失败");
            yield break;
        }
        onDone?.Invoke(resp.hint?.Trim() ?? "", "");
    }

    static UnityWebRequest PostJson(string url, string json)
    {
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
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

    [Serializable]
    public class StoryCreationReplyRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string sceneGuideText;
        public string previousSummary;
        public string gapKind;
        public string roleName;
        public string originalQuestion;
        public string question;
        public string answer;
        public int turnIndex;
        public string gapConversationLog;
    }

    [Serializable]
    public class StoryCreationReplyResult
    {
        public string intent;
        public string acknowledgement;
        public string followUpQuestion;
        public string extractedAnswer;
        public bool conversationDone;
        public string error;
    }

    [Serializable]
    public class StoryCreationSummaryRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string sceneGuideText;
        public string previousSummary;
        public string conversationLog;
    }

    [Serializable]
    class SummaryResponse
    {
        public string summary;
        public string error;
    }

    [Serializable]
    public class StoryCreationPageCaptionRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string sceneGuideText;
        public string previousSummary;
        public string pageSummary;
        public string conversationLog;
        public int maxChars;
    }

    [Serializable]
    class PageCaptionResponse
    {
        public string caption;
        public string error;
    }

    [Serializable]
    public class StoryCreationFreeChatRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string sceneGuideText;
        public string previousSummary;
        public string rosterHint;
        public string userMessage;
    }

    [Serializable]
    class FreeChatResponse
    {
        public string reply;
        public string error;
    }

    [Serializable]
    public class StoryCreationNarrationRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string pageSummary;
    }

    [Serializable]
    class NarrationResponse
    {
        public string narration;
        public string error;
    }

    [Serializable]
    public class StoryCreationRecapRequest
    {
        public string storyTitle;
        public string pageTitle;
        public string pageSummary;
        public string storySoFar;
    }

    [Serializable]
    class RecapResponse
    {
        public string recap;
        public string error;
    }

    [Serializable]
    public class StoryCreationBranchRequest
    {
        public string storyTitle;
        public string nextPageTitle;
        public string pageSummary;
    }

    [Serializable]
    class BranchHintResponse
    {
        public string hint;
        public string error;
    }
}
