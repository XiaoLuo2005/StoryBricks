using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 教程页右侧语音助手「乐乐」：连续监听，直接语音提问（默认无需唤醒词）。需运行 storybricks-tutor-gateway。
/// </summary>
[DisallowMultipleComponent]
public class TutorialVoiceTutorController : MonoBehaviour
{
    [Tooltip("false=直接说话提问；true=需先说「你好乐乐」")]
    public bool requireWakeWord = false;

    [Tooltip("优先使用 /api/tutor/voice-stream 分阶段返回（识别→文字→语音），体感更快")]
    public bool useStreamingVoice = true;

    TutorialStepsConfig _config;
    StepViewerUI _viewer;
    string _baseUrl = "http://127.0.0.1:8787";
    TMP_FontAsset _font;

    TextMeshProUGUI _status;
    TextMeshProUGUI _recordLabel;
    TutorialLelePanelView _panel;

    UnityWebRequest _active;
    GameObject _voiceHost;
    AudioSource _audio;
    StoryCreationVoiceGateway _voiceGateway;
    ContinuousVoiceListener _continuousListener;
    bool _voiceBusy;
    bool _leleOutputting;
    bool _leleAwake;
    bool _bound;
    string _lastLeleSpokenLine = "";
    readonly List<string> _recentLeleLines = new List<string>(4);

    const int MaxTutorOverviewChars = 2500;
    const int MaxRecentLeleLines = 4;
    const float PostTtsEchoGuardSeconds = 0.85f;
    const float PostLeleMicIgnoreSeconds = 0.45f;

    float _ignoreMicUntil;

    public void Initialize(
        TutorialStepsConfig config,
        StepViewerUI viewer,
        string baseUrl,
        TMP_FontAsset font,
        Transform voiceHostParent)
    {
        _config = config;
        _viewer = viewer;
        _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        _font = font;
        EnsureVoiceHost(voiceHostParent);
    }

    public void BindPanel(TutorialLelePanelView panel)
    {
        if (panel == null || !panel.IsComplete)
        {
            Debug.LogError("[TutorialVoiceTutorController] 乐乐面板 UI 不完整，请在 Prefab 里生成 LelePanelRoot 子节点。");
            return;
        }

        _status = panel.statusText;
        _recordLabel = panel.listenStatusLabel;
        _panel = panel;
        panel.EnsureDialogLog();

        var opening = requireWakeWord
            ? $"你好！我是{LeleVoiceAssistant.DisplayName}。先说「{LeleVoiceAssistant.WakePhrase}」唤醒我，再提问。"
            : $"你好！我是{LeleVoiceAssistant.DisplayName}。直接说话提问就行，我会根据当前步骤帮你讲解。";
        panel.DialogLog?.SetOpening(opening);
        RememberLeleLine(opening);

        EnsureVoiceHost(null);
        StartContinuousListening();
        if (!_bound)
        {
            _bound = true;
            StartCoroutine(StartupRoutine());
        }
    }

    void EnsureVoiceHost(Transform parent)
    {
        if (_voiceHost != null)
            return;

        Transform hostParent = parent != null ? parent : transform;
        var existing = hostParent.Find("TutorialVoiceServices");
        _voiceHost = existing != null ? existing.gameObject : new GameObject("TutorialVoiceServices");
        if (existing == null)
            _voiceHost.transform.SetParent(hostParent, false);

        _audio = _voiceHost.GetComponent<AudioSource>();
        if (_audio == null)
            _audio = _voiceHost.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _audio.volume = 1f;

        _voiceGateway = _voiceHost.GetComponent<StoryCreationVoiceGateway>();
        if (_voiceGateway == null)
            _voiceGateway = _voiceHost.AddComponent<StoryCreationVoiceGateway>();
        _voiceGateway.GatewayBaseUrl = _baseUrl;

        _continuousListener = _voiceHost.GetComponent<ContinuousVoiceListener>();
        if (_continuousListener == null)
            _continuousListener = _voiceHost.AddComponent<ContinuousVoiceListener>();
    }

    IEnumerator StartupRoutine()
    {
        yield return CheckGatewayHealth();
        if (!requireWakeWord)
        {
            _leleAwake = true;
            UpdateLeleListenLabel(false);
        }
    }

    IEnumerator CheckGatewayHealth()
    {
        var url = $"{_baseUrl}/health";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            var msg = $"无法连接语音服务（{url}），请启动 storybricks-tutor-gateway";
            Debug.LogWarning($"[TutorialVoiceTutor] {msg}: {req.error}");
            SetStatus("语音服务未连接");
            AppendSystem($"无法连接语音服务（{url}），请启动 storybricks-tutor-gateway");
            yield break;
        }

        var health = JsonUtility.FromJson<GatewayHealthResponse>(req.downloadHandler.text);
        if (!IsGatewayTutorReady(health))
        {
            SetStatus("请在 gateway .env 配置 DEEPSEEK_API_KEY");
            AppendSystem("教程乐乐需要 DEEPSEEK_API_KEY 或 DASHSCOPE_API_KEY。");
            yield break;
        }

        SetStatus(requireWakeWord ? LeleVoiceAssistant.WakeHint : LeleVoiceAssistant.ListeningHint);
    }

    static bool IsGatewayTutorReady(GatewayHealthResponse health)
    {
        if (health == null || !health.ok)
            return false;
        if (health.tutorReady)
            return true;
        return health.hasDeepSeekKey || health.hasDashScopeKey;
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
                if (Time.unscaledTime < _ignoreMicUntil)
                    return;
                if (IsListeningSuppressed())
                    return;

                _voiceBusy = true;
                PauseListening();
                StartCoroutine(HandleLeleUtterance(wav));
            },
            err => SetStatus(err),
            speaking => UpdateLeleListenLabel(speaking));

        if (ok)
        {
            SetStatus(requireWakeWord ? LeleVoiceAssistant.WakeHint : LeleVoiceAssistant.ListeningHint);
            Debug.Log("[TutorialVoiceTutor] 麦克风监听已启动");
        }
        else if (_recordLabel != null)
        {
            _recordLabel.text = "麦克风未就绪";
        }
    }

    void UpdateLeleListenLabel(bool userSpeaking)
    {
        if (_recordLabel == null)
            return;

        if (userSpeaking && !IsListeningSuppressed())
        {
            _recordLabel.text = LeleVoiceAssistant.SpeakingHint;
            SetStatus(LeleVoiceAssistant.ListeningLiveHint);
            return;
        }

        if (IsListeningSuppressed())
        {
            if (_leleOutputting || IsLeleAudioPlaying())
                _recordLabel.text = SpeakingStatus;
            else if (_voiceBusy)
                _recordLabel.text = LeleVoiceAssistant.ThinkingHint;
            return;
        }

        if (requireWakeWord && !_leleAwake)
            _recordLabel.text = LeleVoiceAssistant.WakeHint;
        else
            _recordLabel.text = LeleVoiceAssistant.ListeningHint;
    }

    bool IsLeleAudioPlaying()
    {
        if (_audio != null && _audio.isPlaying)
            return true;
        return _voiceGateway != null && _voiceGateway.IsSpeaking;
    }

    bool IsListeningSuppressed()
    {
        if (_voiceBusy || _leleOutputting)
            return true;
        if (_continuousListener != null && _continuousListener.IsPaused)
            return true;
        return IsLeleAudioPlaying();
    }

    void PauseListening()
    {
        _continuousListener?.Pause();
        UpdateLeleListenLabel(false);
    }

    void MarkLeleOutputting()
    {
        _leleOutputting = true;
        PauseListening();
        if (_recordLabel != null)
            _recordLabel.text = SpeakingStatus;
    }

    void ResumeListening(float extraCooldownSeconds = 0f)
    {
        _leleOutputting = false;
        _voiceBusy = false;
        _continuousListener?.Resume(extraCooldownSeconds);
        UpdateLeleListenLabel(false);
    }

    void ArmPostLeleMicIgnore()
    {
        _ignoreMicUntil = Time.unscaledTime + PostLeleMicIgnoreSeconds;
    }

    void StopContinuousListening()
    {
        _continuousListener?.StopListening();
        if (!requireWakeWord)
            _leleAwake = true;
        else
            _leleAwake = false;
    }

    IEnumerator HandleLeleUtterance(byte[] wavBytes)
    {
        if (requireWakeWord && !_leleAwake)
        {
            yield return HandleWakeFlow(wavBytes);
            yield break;
        }

        yield return RespondWithVoiceCall(wavBytes);
        _leleAwake = !requireWakeWord;
        UpdateLeleListenLabel(false);
        SetStatus(requireWakeWord ? LeleVoiceAssistant.WakeHint : LeleVoiceAssistant.ListeningHint);
    }

    IEnumerator RespondWithVoiceCall(byte[] wavBytes)
    {
        if (useStreamingVoice)
        {
            bool streamOk = false;
            yield return TryStreamingVoiceCall(wavBytes, ok => streamOk = ok);
            if (streamOk)
                yield break;
        }

        yield return RespondWithSingleVoiceCall(wavBytes);
    }

    IEnumerator TryStreamingVoiceCall(byte[] wavBytes, Action<bool> onComplete)
    {
        _voiceBusy = true;
        PauseListening();
        SetStatus(LeleVoiceAssistant.TranscribingHint);
        CancelRequest();

        var form = new WWWForm();
        form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav");
        foreach (var kv in BuildTutorContextFormFields())
            form.AddField(kv.Key, kv.Value);

        var url = $"{_baseUrl}/api/tutor/voice-stream";
        var handler = new TutorVoiceNdjsonDownloadHandler();
        var req = UnityWebRequest.Post(url, form);
        req.downloadHandler = handler;
        _active = req;

        var transcript = "";
        var reply = "";
        var audioBase64 = "";
        var audioFormat = "wav";
        var anyProgress = false;
        var streamDone = false;
        var streamError = "";
        var echoDetected = false;

        var op = req.SendWebRequest();
        while (!op.isDone && !echoDetected)
        {
            handler.DrainEvents(evt => ApplyStreamEvent(
                evt,
                ref transcript,
                ref reply,
                ref audioBase64,
                ref audioFormat,
                ref anyProgress,
                ref streamDone,
                ref streamError,
                ref echoDetected));
            yield return null;
        }

        handler.DrainEvents(evt => ApplyStreamEvent(
            evt,
            ref transcript,
            ref reply,
            ref audioBase64,
            ref audioFormat,
            ref anyProgress,
            ref streamDone,
            ref streamError,
            ref echoDetected));

        if (echoDetected)
            req.Abort();

        var httpResult = req.result;
        var httpError = req.error ?? "";
        _active = null;
        req.Dispose();

        if (echoDetected)
        {
            yield return DiscardEchoUtterance();
            onComplete?.Invoke(true);
            yield break;
        }

        var httpOk = httpResult == UnityWebRequest.Result.Success;
        if (!httpOk && !anyProgress)
        {
            _leleOutputting = false;
            onComplete?.Invoke(false);
            yield break;
        }

        if (!string.IsNullOrEmpty(streamError) || (!streamDone && !httpOk))
        {
            AppendSystem("[错误] " + StripTags(string.IsNullOrEmpty(streamError)
                ? (string.IsNullOrEmpty(httpError) ? "流式语音失败" : httpError)
                : streamError));
            SetStatus("");
            ResumeListening();
            onComplete?.Invoke(true);
            yield break;
        }

        if (!streamDone)
        {
            _leleOutputting = false;
            onComplete?.Invoke(false);
            yield break;
        }

        SetStatus(SpeakingStatus);
        var spoke = false;
        if (!string.IsNullOrEmpty(audioBase64))
        {
            MarkLeleOutputting();
            yield return _voiceGateway.PlayAudioFromBase64(audioBase64, audioFormat, (ok, err) =>
            {
                spoke = ok;
                if (!ok)
                    Debug.LogWarning($"[TutorialVoiceTutor] 流式音频播放失败: {err}");
            });
        }

        if (!spoke && !string.IsNullOrEmpty(reply))
            yield return SpeakLeleLine(reply);

        yield return PostSpeakCooldown();
        yield return FinishVoiceTurn();
        onComplete?.Invoke(true);
    }

    void ApplyStreamEvent(
        TutorVoiceStreamEvent evt,
        ref string transcript,
        ref string reply,
        ref string audioBase64,
        ref string audioFormat,
        ref bool anyProgress,
        ref bool streamDone,
        ref string streamError,
        ref bool echoDetected)
    {
        if (evt == null || string.IsNullOrEmpty(evt.stage))
            return;

        switch (evt.stage)
        {
            case "transcript":
                transcript = (evt.transcript ?? "").Trim();
                anyProgress = true;
                if (ShouldTreatAsSpeakerEcho(transcript))
                {
                    echoDetected = true;
                    return;
                }

                AppendUser(transcript);
                SetStatus(LeleVoiceAssistant.ThinkingHint);
                break;
            case "reply":
                reply = evt.reply ?? "";
                anyProgress = true;
                AppendLele(reply);
                RememberLeleLine(reply);
                MarkLeleOutputting();
                SetStatus(SpeakingStatus);
                break;
            case "audio":
                audioBase64 = evt.audioBase64 ?? "";
                audioFormat = string.IsNullOrEmpty(evt.audioFormat) ? "wav" : evt.audioFormat;
                anyProgress = true;
                break;
            case "done":
                streamDone = true;
                break;
            case "error":
                streamError = evt.error ?? "流式语音失败";
                anyProgress = true;
                break;
        }
    }

    IEnumerator RespondWithSingleVoiceCall(byte[] wavBytes)
    {
        _voiceBusy = true;
        PauseListening();
        SetStatus(LeleVoiceAssistant.ThinkingHint);

        CancelRequest();
        var form = new WWWForm();
        form.AddBinaryData("audio", wavBytes, "voice.wav", "audio/wav");
        foreach (var kv in BuildTutorContextFormFields())
            form.AddField(kv.Key, kv.Value);

        var url = $"{_baseUrl}/api/tutor/voice";
        using var req = UnityWebRequest.Post(url, form);
        _active = req;
        yield return req.SendWebRequest();
        _active = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            var err = string.IsNullOrEmpty(req.error) ? req.downloadHandler?.text : req.error;
            AppendSystem("[网络] " + err);
            SetStatus("网络错误，请检查 gateway");
            ResumeListening();
            yield break;
        }

        var resp = JsonUtility.FromJson<TutorVoiceResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            AppendSystem("[错误] " + StripTags(resp != null ? resp.error : "无效响应"));
            SetStatus("");
            ResumeListening();
            yield break;
        }

        yield return PresentVoiceResponse(resp);
    }

    IEnumerator PresentVoiceResponse(TutorVoiceResponse resp)
    {
        var transcript = (resp.transcript ?? "").Trim();
        if (ShouldTreatAsSpeakerEcho(transcript))
        {
            yield return DiscardEchoUtterance();
            yield break;
        }

        AppendUser(transcript);
        AppendLele(resp.reply);
        RememberLeleLine(resp.reply);
        MarkLeleOutputting();
        SetStatus(SpeakingStatus);

        bool spoke = false;
        if (!string.IsNullOrEmpty(resp.audioBase64))
        {
            MarkLeleOutputting();
            yield return _voiceGateway.PlayAudioFromBase64(resp.audioBase64, resp.audioFormat, (ok, err) =>
            {
                spoke = ok;
                if (!ok)
                    Debug.LogWarning($"[TutorialVoiceTutor] 音频播放失败: {err}");
            });
        }

        if (!spoke)
            yield return SpeakLeleLine(resp.reply);

        yield return PostSpeakCooldown();
        yield return FinishVoiceTurn();
    }

    IEnumerator FinishVoiceTurn()
    {
        if (_audio != null && _audio.isPlaying)
            yield return new WaitWhile(() => _audio != null && _audio.isPlaying);
        if (_voiceGateway != null && _voiceGateway.IsSpeaking)
            yield return new WaitWhile(() => _voiceGateway != null && _voiceGateway.IsSpeaking);

        yield return new WaitForSeconds(PostTtsEchoGuardSeconds);
        ArmPostLeleMicIgnore();
        ResumeListening(0.35f);
    }

    IEnumerator DiscardEchoUtterance()
    {
        Debug.Log("[TutorialVoiceTutor] 丢弃疑似扬声器回声");
        yield return PostSpeakCooldown();
        yield return FinishVoiceTurn();
    }

    bool ShouldTreatAsSpeakerEcho(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return true;

        if (LeleVoiceAssistant.LooksLikeEchoOfAny(transcript, _recentLeleLines))
            return true;

        if (LeleVoiceAssistant.LooksLikeEchoOf(transcript, _lastLeleSpokenLine))
            return true;

        if (LeleVoiceAssistant.LooksLikeEchoOf(transcript, LeleVoiceAssistant.WakeAcknowledgement))
            return true;

        return false;
    }

    void RememberLeleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        line = line.Trim();
        _lastLeleSpokenLine = line;
        _recentLeleLines.RemoveAll(s => string.Equals(s, line, StringComparison.Ordinal));
        _recentLeleLines.Add(line);
        while (_recentLeleLines.Count > MaxRecentLeleLines)
            _recentLeleLines.RemoveAt(0);
    }

    IEnumerator HandleWakeFlow(byte[] wavBytes)
    {
        _voiceBusy = true;
        PauseListening();
        SetStatus(LeleVoiceAssistant.TranscribingHint);

        string transcript = "";
        string error = "";
        var asrCtx = new StoryCreationVoiceGateway.AsrContext { fast = true };
        yield return _voiceGateway.TranscribeWav(wavBytes, asrCtx, (t, e) =>
        {
            transcript = t;
            error = e;
        });

        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(transcript))
        {
            SetStatus(error ?? "没听清，请再说一次");
            ResumeListening();
            yield break;
        }

        if (!LeleVoiceAssistant.ContainsWakeWord(transcript))
        {
            AppendSystem($"请先说「{LeleVoiceAssistant.WakePhrase}」再提问。");
            SetStatus(LeleVoiceAssistant.WakeHint);
            ResumeListening();
            yield break;
        }

        transcript = LeleVoiceAssistant.StripWakePrefix(transcript);
        _leleAwake = true;
        UpdateLeleListenLabel(false);

        if (string.IsNullOrWhiteSpace(transcript) || LeleVoiceAssistant.IsWakeOnlyTranscript(transcript))
        {
            AppendLele(LeleVoiceAssistant.WakeAcknowledgement);
            SetStatus(LeleVoiceAssistant.WakeAcknowledgement);
            RememberLeleLine(LeleVoiceAssistant.WakeAcknowledgement);
            MarkLeleOutputting();
            yield return SpeakLeleLine(LeleVoiceAssistant.WakeAcknowledgement);
            yield return PostSpeakCooldown();
            yield return FinishVoiceTurn();
            yield break;
        }

        if (ShouldTreatAsSpeakerEcho(transcript))
        {
            yield return DiscardEchoUtterance();
            yield break;
        }

        AppendUser(transcript);
        yield return PostTutorReply(transcript);
        _leleAwake = false;
        UpdateLeleListenLabel(false);
        SetStatus(LeleVoiceAssistant.WakeHint);
    }

    static IEnumerator PostSpeakCooldown()
    {
        yield return new WaitForSeconds(0.12f);
    }

    IEnumerator SpeakLeleLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _voiceGateway == null)
            yield break;

        MarkLeleOutputting();

        bool ok = false;
        string err = "";
        yield return _voiceGateway.SpeakText(text.Trim(), (success, e) =>
        {
            ok = success;
            err = e;
        });

        if (!ok)
        {
            Debug.LogWarning($"[TutorialVoiceTutor] TTS 失败: {err}");
            SetStatus(string.IsNullOrEmpty(err) ? "请看文字回答" : $"请看文字（{err}）");
        }
    }

    IEnumerator PostTutorReply(string userMessage)
    {
        CancelRequest();
        SetStatus(LeleVoiceAssistant.ThinkingHint);

        var body = new TutorTextRequest();
        FillTutorContextFields(body);
        body.userMessage = userMessage;
        body.includeTts = true;

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
            AppendSystem("[网络] " + req.error);
            SetStatus("网络错误");
            ResumeListening();
            yield break;
        }

        var resp = JsonUtility.FromJson<TutorTextResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            AppendSystem("[错误] " + StripTags(resp != null ? resp.error : "无效响应"));
            SetStatus("");
            ResumeListening();
            yield break;
        }

        AppendLele(resp.reply);
        RememberLeleLine(resp.reply);
        MarkLeleOutputting();
        SetStatus(SpeakingStatus);

        bool spoke = false;
        if (!string.IsNullOrEmpty(resp.audioBase64))
        {
            MarkLeleOutputting();
            yield return _voiceGateway.PlayAudioFromBase64(resp.audioBase64, resp.audioFormat, (ok, err) =>
            {
                spoke = ok;
                if (!ok)
                    Debug.LogWarning($"[TutorialVoiceTutor] TTS 播放失败: {err}");
            });
        }

        if (!spoke)
            yield return SpeakLeleLine(resp.reply);

        yield return PostSpeakCooldown();
        yield return FinishVoiceTurn();
    }

    void CancelRequest()
    {
        if (_active == null)
            return;
        _active.Abort();
        _active.Dispose();
        _active = null;
    }

    void AppendUser(string text) => _panel?.DialogLog?.AppendUser(text);

    void AppendLele(string text) => _panel?.DialogLog?.AppendLele(text);

    void AppendSystem(string text) => _panel?.DialogLog?.AppendSystem(text);

    const string SpeakingStatus = "乐乐正在说…";

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

    static string ParseAsrError(string responseText, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return string.IsNullOrWhiteSpace(fallback) ? "识别失败" : fallback;

        var resp = JsonUtility.FromJson<LeleAsrResponse>(responseText);
        if (!string.IsNullOrWhiteSpace(resp?.error))
            return StoryCreationVoiceGateway.FriendlyAsrError(resp.error.Trim());

        return string.IsNullOrWhiteSpace(fallback) ? "识别失败" : fallback;
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
    class GatewayHealthResponse
    {
        public bool ok;
        public bool tutorReady;
        public bool storyCreationReady;
        public bool hasDeepSeekKey;
        public bool hasDashScopeKey;
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
        public bool includeTts;
    }

    [Serializable]
    class TutorTextResponse
    {
        public string reply;
        public string audioBase64;
        public string audioFormat;
        public string error;
    }

    [Serializable]
    class TutorVoiceResponse
    {
        public string transcript;
        public string reply;
        public string audioBase64;
        public string audioFormat;
        public string error;
    }

    [Serializable]
    class LeleAsrResponse
    {
        public string transcript;
        public string error;
    }
}
