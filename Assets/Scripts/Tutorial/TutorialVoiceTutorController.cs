using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 教程页右侧 AI 助教：文字提问 + 按住说话（WAV 上传网关）。需运行 Server/storybricks-tutor-gateway 并配置 DASHSCOPE_API_KEY。
/// </summary>
[DisallowMultipleComponent]
public class TutorialVoiceTutorController : MonoBehaviour
{
    TutorialStepsConfig _config;
    StepViewerUI _viewer;
    string _baseUrl = "http://127.0.0.1:8787";
    Font _font;

    Text _output;
    Text _status;
    InputField _input;
    AudioSource _audio;
    Text _recordLabel;

    UnityWebRequest _active;

    const int MicSampleRate = 16000;
    const int MicMaxSeconds = 12;
    const int MaxTutorOverviewChars = 12000;
    string _micDevice;
    AudioClip _micClip;
    bool _micRecording;

    public void Initialize(TutorialStepsConfig config, StepViewerUI viewer, string baseUrl, Font font)
    {
        _config = config;
        _viewer = viewer;
        _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        _font = font;
    }

    public void BuildPanel(RectTransform panelRt)
    {
        var font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var bg = panelRt.gameObject.AddComponent<Image>();
        bg.color = new Color32(40, 44, 52, 230);
        bg.raycastTarget = true;

        var v = panelRt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(16, 16, 16, 16);
        v.spacing = 10f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        var title = CreateText(panelRt, "Title", "AI 助教", 26, FontStyle.Bold, TextAnchor.MiddleLeft, font);
        ((Graphic)title).color = Color.white;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

        var scrollGo = CreateChild(panelRt, "Scroll");
        var sle = scrollGo.gameObject.AddComponent<LayoutElement>();
        sle.preferredHeight = 300f;
        sle.flexibleHeight = 1f;
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        StretchFull(scrollRt);

        var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateChild(scrollRt, "Viewport");
        StretchFull(viewport);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateChild(viewport, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(0f, 0f);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        var csfRoot = content.gameObject.AddComponent<ContentSizeFitter>();
        csfRoot.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csfRoot.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _output = CreateScrollOutputText(content, font);
        _output.supportRichText = true;
        _output.color = new Color32(230, 233, 240, 255);
        _output.horizontalOverflow = HorizontalWrapMode.Wrap;
        _output.verticalOverflow = VerticalWrapMode.Overflow;
        var outLe = _output.gameObject.AddComponent<LayoutElement>();
        outLe.flexibleWidth = 1f;
        outLe.minHeight = 8f;
        var outCsf = _output.gameObject.AddComponent<ContentSizeFitter>();
        outCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        outCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = crt;

        _input = CreateInputField(panelRt, "Input", font);
        _input.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        var row = CreateChild(panelRt, "Row");
        row.gameObject.AddComponent<HorizontalLayoutGroup>().spacing = 12f;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

        var sendGo = CreateChild(row, "Send");
        sendGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
        sendGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
        var sendImg = sendGo.gameObject.AddComponent<Image>();
        sendImg.color = new Color32(90, 130, 220, 255);
        var sendBtn = sendGo.gameObject.AddComponent<Button>();
        sendBtn.targetGraphic = sendImg;
        sendBtn.onClick.AddListener(OnSendClicked);
        var sendTx = CreateText(sendGo, "Tx", "发送", 22, FontStyle.Normal, TextAnchor.MiddleCenter, font);
        StretchFull(sendTx.rectTransform);
        sendTx.color = Color.white;

        var recGo = CreateChild(row, "Record");
        recGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;
        recGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
        var recImg = recGo.gameObject.AddComponent<Image>();
        recImg.color = new Color32(70, 120, 200, 255);
        recGo.gameObject.AddComponent<Button>().targetGraphic = recImg;
        _recordLabel = CreateText(recGo, "Tx", "按住说话", 22, FontStyle.Normal, TextAnchor.MiddleCenter, font);
        StretchFull(_recordLabel.rectTransform);
        _recordLabel.color = Color.white;
        var push = recGo.gameObject.AddComponent<TutorPushToTalk>();
        push.host = this;

        _status = CreateText(panelRt, "Status", "", 18, FontStyle.Normal, TextAnchor.MiddleLeft, font);
        _status.color = new Color32(180, 188, 200, 255);
        _status.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
    }

    void OnDestroy()
    {
        CancelRequest();
        StopMicIfAny();
    }

    void OnDisable()
    {
        CancelRequest();
        StopMicIfAny();
    }

    public void OnRecordDown()
    {
        if (_micRecording)
            return;
        if (!EnsureMicPermission())
        {
            SetStatus("需要麦克风权限");
            return;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            SetStatus("未检测到麦克风");
            return;
        }

        _micDevice = Microphone.devices[0];
        _micClip = Microphone.Start(_micDevice, false, MicMaxSeconds, MicSampleRate);
        _micRecording = true;
        if (_recordLabel != null)
            _recordLabel.text = "松开上传";
        SetStatus("正在听…");
    }

    public void OnRecordUp()
    {
        if (!_micRecording)
            return;
        _micRecording = false;
        if (_recordLabel != null)
            _recordLabel.text = "按住说话";

        if (_micClip == null || string.IsNullOrEmpty(_micDevice))
        {
            SetStatus("录音失败");
            return;
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
            SetStatus("太短了，请再试一次");
            return;
        }

        var mono = channels > 1 ? DownmixToMono(data, channels) : data;
        var wav = PcmFloatWavEncoder.EncodeMono16(mono, MicSampleRate);
        StartCoroutine(PostVoice(wav));
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

    bool EnsureMicPermission()
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

    void StopMicIfAny()
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

    void OnSendClicked()
    {
        var msg = _input != null ? _input.text.Trim() : "";
        if (string.IsNullOrEmpty(msg))
        {
            SetStatus("请先输入问题");
            return;
        }

        StartCoroutine(PostText(msg));
    }

    IEnumerator PostText(string userMessage)
    {
        CancelRequest();
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
            yield break;
        }

        var resp = JsonUtility.FromJson<TutorTextResponse>(req.downloadHandler.text);
        if (resp == null)
        {
            SetStatus("响应无效");
            yield break;
        }

        if (!string.IsNullOrEmpty(resp.error))
        {
            AppendOutput("\n[错误] " + StripTags(resp.error));
            SetStatus("");
            yield break;
        }

        AppendOutput("\n你：" + userMessage);
        AppendOutput("\n助教：" + resp.reply);
        SetStatus("");
        if (_input != null)
            _input.text = "";
    }

    IEnumerator PostVoice(byte[] wavBytes)
    {
        CancelRequest();
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
            yield break;
        }

        var resp = JsonUtility.FromJson<TutorVoiceResponse>(req.downloadHandler.text);
        if (resp == null || !string.IsNullOrEmpty(resp.error))
        {
            AppendOutput("\n[错误] " + StripTags(resp != null ? resp.error : "无效响应"));
            SetStatus("");
            yield break;
        }

        AppendOutput("\n你（语音）：" + resp.transcript);
        AppendOutput("\n助教：" + resp.reply);
        SetStatus("");

        if (!string.IsNullOrEmpty(resp.audioBase64))
            yield return PlayWavBase64(resp.audioBase64);
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

    static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Text CreateScrollOutputText(Transform parent, Font font)
    {
        var go = new GameObject("Output", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);

        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = 21;
        t.fontStyle = FontStyle.Normal;
        t.color = new Color32(40, 44, 52, 255);
        t.text = "你好！可以打字提问，或按住「按住说话」录音后松开上传。";
        t.alignment = TextAnchor.UpperLeft;
        return t;
    }

    static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, TextAnchor align, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = new Color32(40, 44, 52, 255);
        t.text = text;
        t.alignment = align;
        StretchFull(go.GetComponent<RectTransform>());
        return t;
    }

    static InputField CreateInputField(Transform parent, string name, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = new Color32(255, 255, 255, 245);
        var field = go.AddComponent<InputField>();
        field.lineType = InputField.LineType.SingleLine;
        field.characterLimit = 512;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.layer = LayerMask.NameToLayer("UI");
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.fontSize = 22;
        text.color = new Color32(40, 44, 52, 255);
        text.supportRichText = false;
        field.textComponent = text;

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.layer = LayerMask.NameToLayer("UI");
        phGo.transform.SetParent(go.transform, false);
        StretchFull(phGo.GetComponent<RectTransform>());
        var ph = phGo.AddComponent<Text>();
        ph.font = font;
        ph.fontSize = 22;
        ph.color = new Color32(140, 145, 155, 200);
        ph.text = "输入问题…";
        field.placeholder = ph;

        return field;
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
        return new System.Collections.Generic.Dictionary<string, string>
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
}

sealed class TutorPushToTalk : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public TutorialVoiceTutorController host;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (host != null)
            host.OnRecordDown();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (host != null)
            host.OnRecordUp();
    }
}
