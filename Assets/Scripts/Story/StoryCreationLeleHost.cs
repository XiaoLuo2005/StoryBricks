using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 故事创作页常驻「乐乐」面板：边玩边听，摆好后一次复述确认。
/// </summary>
[DisallowMultipleComponent]
public class StoryCreationLeleHost : MonoBehaviour
{
    const string SpeakingStatus = "稍等，乐乐正在说话…";

    StoryCreationVoiceGateway _gateway;
    TutorialLelePanelView _panel;
    bool _freeChatEnabled;
    bool _voiceBusy;
    bool _speechPumpRunning;
    readonly Queue<string> _speechQueue = new Queue<string>();
    string _storyTitle = "";
    string _pageTitle = "";
    string _sceneGuideText = "";
    string _rosterHint = "";
    readonly StringBuilder _dialog = new StringBuilder();
    readonly StringBuilder _storyDraft = new StringBuilder();

    public TutorialLelePanelView Panel => _panel;
    public bool IsFreeChatEnabled => _freeChatEnabled;
    public string RosterHint => _rosterHint ?? "";

    public void Initialize(RectTransform canvasRoot, StoryCreationVoiceGateway gateway)
    {
        _gateway = gateway;
        if (_gateway == null)
            _gateway = GetComponent<StoryCreationVoiceGateway>();

        var leleRoot = CreateLeleRoot(canvasRoot);
        _panel = TutorialLelePanelUiBuilder.Build(leleRoot);
        StretchStoryCreationDialogScroll(_panel);
        _panel.EnsureDialogLog();

        ResetDialog(
            $"你好！我是{LeleVoiceAssistant.DisplayName}。边摆积木边告诉我就行，摆好了点「这页摆好了」。");
    }

    public void SetPageContext(string storyTitle, string pageTitle, string sceneGuideText)
    {
        _storyTitle = storyTitle ?? "";
        _pageTitle = pageTitle ?? "";
        _sceneGuideText = sceneGuideText ?? "";
        ClearStoryDraft();
    }

    public void SetRosterHint(string hint)
    {
        _rosterHint = hint ?? "";
    }

    public void ResetDialog(string openingLine)
    {
        _dialog.Clear();
        ClearStoryDraft();
        _panel?.EnsureDialogLog();
        _panel?.DialogLog?.SetOpening(openingLine ?? "");
        if (!string.IsNullOrWhiteSpace(openingLine))
            _dialog.AppendLine(openingLine.Trim());
    }

    public void AppendLele(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        _dialog.AppendLine($"{LeleVoiceAssistant.DisplayName}：{line.Trim()}");
        _panel?.DialogLog?.AppendLele(line.Trim());
    }

    public void AppendChild(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        _dialog.AppendLine($"你：{line.Trim()}");
        _panel?.DialogLog?.AppendUser(line.Trim());
    }

    public string BuildConversationLog() => _dialog.ToString();

    public string GetStoryDraft() => _storyDraft.ToString().Trim();

    public void ClearStoryDraft() => _storyDraft.Clear();

    public void AppendStoryDraft(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        if (_storyDraft.Length > 0)
            _storyDraft.AppendLine();
        _storyDraft.Append(line.Trim());
    }

    public string BuildExtractConversationLog()
    {
        var draft = GetStoryDraft();
        var dialog = BuildConversationLog();
        if (string.IsNullOrWhiteSpace(draft))
            return dialog;
        if (string.IsNullOrWhiteSpace(dialog))
            return draft;
        return draft + "\n---\n" + dialog;
    }

    public void SetStatus(string text)
    {
        if (_panel?.statusText != null)
            _panel.statusText.text = text ?? "";
    }

    public void SetListenLabel(string text)
    {
        if (_panel?.listenStatusLabel != null)
            _panel.listenStatusLabel.text = text ?? "";
    }

    public void SetFreeChatEnabled(bool enabled)
    {
        _freeChatEnabled = enabled;
        if (enabled)
            StartFreeListening();
        else
            StopFreeListening();
    }

    public IEnumerator SpeakLeleLine(string text, bool appendToDialog = true)
    {
        if (string.IsNullOrWhiteSpace(text) || _gateway == null)
            yield break;

        if (appendToDialog)
            AppendLele(text);

        bool wasListening = _gateway.IsAnswerListening;
        if (wasListening)
            SetListenLabel(SpeakingStatus);

        bool ok = false;
        yield return _gateway.SpeakText(text, (success, _) => ok = success);
        if (!ok)
            SetStatus("语音播放失败，请看文字");
        else if (_freeChatEnabled && wasListening)
            RefreshListeningUi();
    }

    public void ReactPlacement(string line)
    {
        if (!_freeChatEnabled || string.IsNullOrWhiteSpace(line))
            return;

        _speechQueue.Enqueue(line.Trim());
        if (!_speechPumpRunning)
            StartCoroutine(ProcessSpeechQueueCoroutine());
    }

    IEnumerator ProcessSpeechQueueCoroutine()
    {
        _speechPumpRunning = true;
        while (_speechQueue.Count > 0)
        {
            var line = _speechQueue.Dequeue();
            yield return SpeakLeleLine(line, appendToDialog: true);
        }

        _speechPumpRunning = false;
        if (_freeChatEnabled)
            RefreshListeningUi();
    }

    void StartFreeListening()
    {
        if (_gateway == null)
            return;

        _gateway.StopAnswerListening();
        bool ok = _gateway.StartAnswerListening(
            wav =>
            {
                if (_voiceBusy && _gateway != null && !_gateway.IsSpeaking)
                    return;
                StartCoroutine(HandleFreeUtterance(wav));
            },
            err => SetStatus(err),
            speaking => UpdateListeningLabel(speaking));

        if (ok)
            RefreshListeningUi();
        else
            SetStatus("无法开始监听，请检查麦克风权限和设备。");
    }

    void StopFreeListening()
    {
        _speechQueue.Clear();
        _speechPumpRunning = false;
        _gateway?.StopAnswerListening();
        SetListenLabel("");
    }

    public void RefreshListeningUi()
    {
        SetListenLabel(LeleVoiceAssistant.ListeningHint);
        SetStatus("边玩边说，乐乐都在听");
    }

    void UpdateListeningLabel(bool speaking)
    {
        if (_gateway != null && _gateway.IsAnswerListening && IsListenerPaused())
        {
            SetListenLabel(SpeakingStatus);
            return;
        }

        SetListenLabel(speaking ? LeleVoiceAssistant.SpeakingHint : LeleVoiceAssistant.ListeningHint);
    }

    bool IsListenerPaused()
    {
        var listener = GetComponent<ContinuousVoiceListener>();
        return listener != null && listener.IsActive && listener.IsPaused;
    }

    IEnumerator HandleFreeUtterance(byte[] wav)
    {
        if (!_freeChatEnabled || _gateway == null)
            yield break;

        if (_voiceBusy)
        {
            if (_gateway.IsSpeaking)
                _gateway.StopPlayback();
            else
            {
                SetStatus("请等乐乐说完，再跟她说");
                yield break;
            }
        }

        _voiceBusy = true;
        _gateway.PauseAnswerListening();
        SetStatus(LeleVoiceAssistant.TranscribingHint);
        SetListenLabel(LeleVoiceAssistant.TranscribingHint);

        string transcript = "";
        string error = "";
        var asrCtx = new StoryCreationVoiceGateway.AsrContext { fast = true };
        yield return _gateway.TranscribeWav(wav, asrCtx, (t, e) =>
        {
            transcript = t;
            error = e;
        });

        if (string.IsNullOrWhiteSpace(transcript))
        {
            SetStatus(string.IsNullOrEmpty(error) ? "没听清，再说一次吧" : error);
            FinishFreeUtterance();
            yield break;
        }

        AppendChild(transcript);
        AppendStoryDraft(transcript);

        SetStatus(LeleVoiceAssistant.ThinkingHint);
        SetListenLabel(LeleVoiceAssistant.ThinkingHint);

        var req = new StoryCreationVoiceGateway.StoryCreationFreeChatRequest
        {
            storyTitle = _storyTitle,
            pageTitle = _pageTitle,
            sceneGuideText = _sceneGuideText,
            previousSummary = StorySessionCache.BuildPreviousPagesSummary(),
            rosterHint = _rosterHint,
            userMessage = transcript,
        };

        string reply = "";
        yield return _gateway.FetchFreeChatReply(req, (r, e) =>
        {
            reply = r;
            if (!string.IsNullOrEmpty(e))
                SetStatus(e);
        });

        if (!string.IsNullOrWhiteSpace(reply))
        {
            SetListenLabel(SpeakingStatus);
            yield return SpeakLeleLine(reply, appendToDialog: true);
        }

        FinishFreeUtterance();
    }

    void FinishFreeUtterance()
    {
        _voiceBusy = false;
        if (!_freeChatEnabled || _gateway == null)
            return;

        if (_gateway.IsAnswerListening && IsListenerPaused())
            _gateway.ResumeAnswerListening();
        else if (!_gateway.IsAnswerListening)
            StartFreeListening();
        else
            RefreshListeningUi();
    }

    static RectTransform CreateLeleRoot(RectTransform canvasRoot)
    {
        var go = new GameObject("LelePanelRoot", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRoot, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(460f, 272f);
        rt.anchoredPosition = new Vector2(
            24f,
            StoryCreationPageUiBuilder.BottomInset + StoryCreationPageUiBuilder.PrimaryButtonSize.y + 140f);
        return rt;
    }

    static void StretchStoryCreationDialogScroll(TutorialLelePanelView panel)
    {
        if (panel?.dialogScroll == null)
            return;

        var scrollRt = panel.dialogScroll.GetComponent<RectTransform>();
        if (scrollRt == null)
            return;

        scrollRt.offsetMin = new Vector2(14f, 92f);
        scrollRt.offsetMax = new Vector2(-14f, -108f);
    }

    void OnDisable() => StopFreeListening();
}
