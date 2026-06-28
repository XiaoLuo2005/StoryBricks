using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 教程页右侧语音助手「乐乐」：说「你好乐乐」唤醒后实时监听。需运行 storybricks-tutor-gateway。
/// </summary>
[DisallowMultipleComponent]
public class TutorialVoiceTutorController : MonoBehaviour
{
    TutorialStepsConfig _config;
    StepViewerUI _viewer;
    string _baseUrl = "http://127.0.0.1:8787";
    TMP_FontAsset _font;

    TextMeshProUGUI _output;
    TextMeshProUGUI _status;
    AudioSource _audio;
    TextMeshProUGUI _recordLabel;

    UnityWebRequest _active;
    ContinuousVoiceListener _continuousListener;
    bool _voiceBusy;
    bool _leleAwake;

    const int MaxTutorOverviewChars = 12000;

    public void Initialize(TutorialStepsConfig config, StepViewerUI viewer, string baseUrl, TMP_FontAsset font)
    {
        _config = config;
        _viewer = viewer;
        _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        _font = font;
    }

    public void BindPanel(TutorialLelePanelView panel)
    {
        if (panel == null || !panel.IsComplete)
        {
            Debug.LogError("[TutorialVoiceTutorController] 乐乐面板 UI 不完整，请在 Prefab 里生成 LelePanelRoot 子节点。");
            return;
        }

        _output = panel.dialogOutput;
        _status = panel.statusText;
        _recordLabel = panel.listenStatusLabel;

        if (_output != null && string.IsNullOrWhiteSpace(_output.text))
        {
            _output.text =
                $"你好！我是{LeleVoiceAssistant.DisplayName}。先说「{LeleVoiceAssistant.WakePhrase}」唤醒我，再提问。";
        }

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;

        _continuousListener = GetComponent<ContinuousVoiceListener>();
        if (_continuousListener == null)
            _continuousListener = gameObject.AddComponent<ContinuousVoiceListener>();

        StartContinuousListening();
    }

    [System.Obsolete("Use BindPanel with TutorialLelePanelView from Prefab instead.")]
    public void BuildPanel(RectTransform panelRt)
    {
        var panelView = TutorialLelePanelUiBuilder.Build(panelRt);
        BindPanel(panelView);
    }

    void OnDestroy()
    {
        CancelRequest();
        StopContinuousListening();
    }

    void OnDisable()
    {
        CancelRequest();
        StopContinuousListening();
    }

    void StartContinuousListening()
    {
        if (_continuousListener == null)
            return;

        StopContinuousListening();
        bool ok = _continuousListener.StartListening(
            wav =>
            {
                if (_voiceBusy)
                    return;
                StartCoroutine(HandleLeleUtterance(wav));
            },
            err => SetStatus(err),
            speaking => UpdateLeleListenLabel(speaking));

        if (ok)
            SetStatus(LeleVoiceAssistant.WakeHint);
        else if (_recordLabel != null)
            _recordLabel.text = "麦克风未就绪";
    }

    void UpdateLeleListenLabel(bool speaking)
    {
        if (_recordLabel == null)
            return;

        if (speaking)
        {
            _recordLabel.text = LeleVoiceAssistant.SpeakingHint;
            return;
        }

        _recordLabel.text = _leleAwake
            ? LeleVoiceAssistant.ListeningHint
            : LeleVoiceAssistant.WakeHint;
    }

    void StopContinuousListening()
    {
        _continuousListener?.StopListening();
        _leleAwake = false;
    }

    IEnumerator HandleLeleUtterance(byte[] wavBytes)
    {
        _voiceBusy = true;
        _continuousListener?.Pause();
        SetStatus($"{LeleVoiceAssistant.DisplayName}在听…");

        string transcript = "";
        string error = "";
        yield return TranscribeUtterance(wavBytes, (t, e) =>
        {
            transcript = t;
            error = e;
        });

        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(transcript))
        {
            SetStatus(error ?? "没听清，请再说一次");
            _voiceBusy = false;
            _continuousListener?.Resume();
            yield break;
        }

        if (!_leleAwake)
        {
            if (!LeleVoiceAssistant.ContainsWakeWord(transcript))
            {
                SetStatus(LeleVoiceAssistant.WakeHint);
                _voiceBusy = false;
                _continuousListener?.Resume();
                yield break;
            }

            transcript = LeleVoiceAssistant.StripWakePrefix(transcript);
            _leleAwake = true;
            UpdateLeleListenLabel(false);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                AppendOutput($"\n{LeleVoiceAssistant.DisplayName}：{LeleVoiceAssistant.WakeAcknowledgement}");
                SetStatus($"{LeleVoiceAssistant.DisplayName}：{LeleVoiceAssistant.WakeAcknowledgement}");
                yield return SpeakLeleLine(LeleVoiceAssistant.WakeAcknowledgement);
                _voiceBusy = false;
                _continuousListener?.Resume();
                yield break;
            }
        }

        _voiceBusy = false;
        yield return PostText(transcript);
        _leleAwake = false;
        UpdateLeleListenLabel(false);
        SetStatus(LeleVoiceAssistant.WakeHint);
    }

    IEnumerator TranscribeUtterance(byte[] wavBytes, Action<string, string> onDone)
    {
        var form = new WWWForm();
        form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav");
        var url = $"{_baseUrl}/api/story-creation/asr";
        using var req = UnityWebRequest.Post(url, form);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke("", req.error);
            yield break;
        }

        var resp = JsonUtility.FromJson<LeleAsrResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            onDone?.Invoke("", resp?.error ?? "识别失败");
            yield break;
        }

        onDone?.Invoke(resp.transcript?.Trim() ?? "", "");
    }

    IEnumerator SpeakLeleLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var body = JsonUtility.ToJson(new LeleTtsRequest { text = text.Trim() });
        var url = $"{_baseUrl}/api/story-creation/tts";
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            yield break;

        var resp = JsonUtility.FromJson<LeleTtsResponse>(req.downloadHandler.text);
        if (resp == null || string.IsNullOrEmpty(resp.audioBase64))
            yield break;

        yield return PlayWavBase64(resp.audioBase64);
    }

    IEnumerator PostText(string userMessage)
    {
        CancelRequest();
        _voiceBusy = true;
        _continuousListener?.Pause();
        SetStatus("思考中…");
        var body = new TutorTextRequest();
        FillTutorContextFields(body);
        body.userMessage = userMessage;

        var json = JsonUtility.ToJson(body);
        var url = $"{_baseUrl}/api/tutor/text";
        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            AppendOutput("\n[网络] " + req.error);
            SetStatus("");
            _voiceBusy = false;
            _continuousListener?.Resume();
            yield break;
        }

        var resp = JsonUtility.FromJson<TutorTextResponse>(req.downloadHandler.text);
        if (resp == null)
        {
            SetStatus("响应无效");
            _voiceBusy = false;
            _continuousListener?.Resume();
            yield break;
        }

        if (!string.IsNullOrEmpty(resp.error))
        {
            AppendOutput("\n[错误] " + StripTags(resp.error));
            SetStatus("");
            _voiceBusy = false;
            _continuousListener?.Resume();
            yield break;
        }

        AppendOutput("\n你：" + userMessage);
        AppendOutput($"\n{LeleVoiceAssistant.DisplayName}：" + resp.reply);
        SetStatus(LeleVoiceAssistant.WakeHint);
        _voiceBusy = false;
        _leleAwake = false;
        UpdateLeleListenLabel(false);
        _continuousListener?.Resume();
    }

    IEnumerator PostVoice(byte[] wavBytes)
    {
        CancelRequest();
        _voiceBusy = true;
        _continuousListener?.Pause();
        SetStatus("识别语音并回答…");
        var form = new WWWForm();
        form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav");
        var fields = BuildTutorContextFormFields();
        foreach (var kv in fields)
            form.AddField(kv.Key, kv.Value);

        var url = $"{_baseUrl}/api/tutor/voice";
        using var req = UnityWebRequest.Post(url, form);
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            AppendOutput("\n[网络] " + req.error);
            SetStatus("");
            _voiceBusy = false;
            _continuousListener?.Resume();
            yield break;
        }

        var resp = JsonUtility.FromJson<TutorVoiceResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            AppendOutput("\n[错误] " + StripTags(resp != null ? resp.error : "无效响应"));
            SetStatus("");
            _voiceBusy = false;
            _continuousListener?.Resume();
            yield break;
        }

        AppendOutput("\n你（语音）：" + resp.transcript);
        AppendOutput($"\n{LeleVoiceAssistant.DisplayName}：" + resp.reply);
        SetStatus(LeleVoiceAssistant.WakeHint);

        if (!string.IsNullOrEmpty(resp.audioBase64))
            yield return PlayWavBase64(resp.audioBase64);

        _voiceBusy = false;
        _leleAwake = false;
        UpdateLeleListenLabel(false);
        _continuousListener?.Resume();
    }

    IEnumerator PlayWavBase64(string b64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(b64);
        }
        catch
        {
            yield break;
        }

        var path = Path.Combine(Application.persistentDataPath, "tutor_tts_last.wav");
        try
        {
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
            yield break;
        }

        var uri = new Uri(path).AbsoluteUri;
        using var u = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
        yield return u.SendWebRequest();
        if (u.result != UnityWebRequest.Result.Success || _audio == null)
            yield break;

        var clip = DownloadHandlerAudioClip.GetContent(u);
        if (clip == null)
            yield break;

        _audio.clip = clip;
        _audio.Play();
    }

    void CancelRequest()
    {
        if (_active == null)
            return;
        _active.Abort();
        _active.Dispose();
        _active = null;
    }

    void AppendOutput(string line)
    {
        if (_output == null)
            return;
        _output.text += line;
        var contentRt = _output.transform.parent as RectTransform;
        if (contentRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        Canvas.ForceUpdateCanvases();
    }

    void SetStatus(string s)
    {
        if (_status != null)
            _status.text = s ?? "";
    }

    static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("<", "‹");
    }

    void FillTutorContextFields(TutorTextRequest r)
    {
        r.tutorialTitle = _config != null ? _config.title : "";
        r.stepIndex = _viewer != null ? _viewer.CurrentStepIndex : 0;
        r.stepCount = _viewer != null ? _viewer.StepCount : 1;
        r.stepHint = _viewer != null ? _viewer.GetCurrentStepHint() : "";
        r.tutorialTutorOverview = GetTutorialTutorOverviewCapped();
        var d = _viewer != null ? _viewer.GetCurrentStepTutorDetail() : null;
        if (d != null)
        {
            r.stepGoal = d.stepGoal ?? "";
            r.stepPartsUsed = d.partsUsed ?? "";
            r.stepKeyActions = d.keyActions ?? "";
            r.stepPitfalls = d.pitfalls ?? "";
        }
        else
        {
            r.stepGoal = "";
            r.stepPartsUsed = "";
            r.stepKeyActions = "";
            r.stepPitfalls = "";
        }
    }

    string GetTutorialTutorOverviewCapped()
    {
        if (_config == null || _config.tutorialTutorOverviewText == null)
            return "";
        var t = _config.tutorialTutorOverviewText.text;
        if (string.IsNullOrEmpty(t))
            return "";
        t = t.Trim();
        if (t.Length <= MaxTutorOverviewChars)
            return t;
        return t.Substring(0, MaxTutorOverviewChars) + "\n…(总览已截断)";
    }

    Dictionary<string, string> BuildTutorContextFormFields()
    {
        var tmp = new TutorTextRequest();
        FillTutorContextFields(tmp);
        return new Dictionary<string, string>
        {
            ["tutorialTitle"] = tmp.tutorialTitle,
            ["stepIndex"] = tmp.stepIndex.ToString(),
            ["stepCount"] = tmp.stepCount.ToString(),
            ["stepHint"] = tmp.stepHint ?? "",
            ["tutorialTutorOverview"] = tmp.tutorialTutorOverview ?? "",
            ["stepGoal"] = tmp.stepGoal ?? "",
            ["stepPartsUsed"] = tmp.stepPartsUsed ?? "",
            ["stepKeyActions"] = tmp.stepKeyActions ?? "",
            ["stepPitfalls"] = tmp.stepPitfalls ?? "",
        };
    }

    [Serializable]
    class TutorTextRequest
    {
        public string tutorialTitle;
        public int stepIndex;
        public int stepCount;
        public string stepHint;
        public string tutorialTutorOverview;
        public string stepGoal;
        public string stepPartsUsed;
        public string stepKeyActions;
        public string stepPitfalls;
        public string userMessage;
    }

    [Serializable]
    class TutorTextResponse
    {
        public string reply;
        public string error;
    }

    [Serializable]
    class TutorVoiceResponse
    {
        public string transcript;
        public string reply;
        public string audioBase64;
        public string error;
    }

    [Serializable]
    class LeleAsrResponse
    {
        public string transcript;
        public string error;
    }

    [Serializable]
    class LeleTtsRequest
    {
        public string text;
    }

    [Serializable]
    class LeleTtsResponse
    {
        public string audioBase64;
        public string error;
    }
}
