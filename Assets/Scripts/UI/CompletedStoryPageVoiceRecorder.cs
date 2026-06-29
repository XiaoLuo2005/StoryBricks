using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>「我的故事」阅读页：用户为本页故事录音、重录、播放。</summary>
[DisallowMultipleComponent]
public class CompletedStoryPageVoiceRecorder : MonoBehaviour
{
    const int SampleRate = 16000;
    const int MaxRecordSeconds = 90;

    AudioSource _audio;
    string _saveId;
    int _pageIndex;
    CompletedStoryStore.CompletedStoryPageFile[] _pages;
    Text _statusLabel;
    Button _recordButton;
    Button _playButton;
    Button _rerecordButton;

    string _micDevice;
    AudioClip _micClip;
    bool _recording;
    Coroutine _recordRoutine;

    public void Bind(
        string saveId,
        int pageIndex,
        CompletedStoryStore.CompletedStoryPageFile[] pages,
        Button recordButton,
        Button playButton,
        Button rerecordButton,
        Text statusLabel)
    {
        _saveId = saveId;
        _pageIndex = pageIndex;
        _pages = pages;
        _recordButton = recordButton;
        _playButton = playButton;
        _rerecordButton = rerecordButton;
        _statusLabel = statusLabel;

        EnsureAudio();
        RefreshUi();
    }

    public void RefreshUi()
    {
        bool hasRecording = !string.IsNullOrWhiteSpace(GetCurrentRecordingPath());
        SetStatus(hasRecording ? "已保存你的朗读" : "让我们一起朗读吧~");
        if (_playButton != null)
            _playButton.interactable = hasRecording && !_recording;
        if (_rerecordButton != null)
            _rerecordButton.interactable = hasRecording && !_recording;
        UpdateRecordButtonLabel();
    }

    public void OnRecordClicked()
    {
        if (_recording)
        {
            StopRecordingAndSave();
            return;
        }

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

        _audio?.Stop();
        _micDevice = Microphone.devices[0];
        _micClip = Microphone.Start(_micDevice, false, MaxRecordSeconds, SampleRate);
        _recording = true;
        SetStatus("正在录音… 再点一次结束");
        UpdateRecordButtonLabel();
        if (_playButton != null)
            _playButton.interactable = false;
        if (_rerecordButton != null)
            _rerecordButton.interactable = false;

        if (_recordRoutine != null)
            StopCoroutine(_recordRoutine);
        _recordRoutine = StartCoroutine(AutoStopRecordingAfterTimeout());
    }

    public void OnPlayClicked()
    {
        if (_recording)
            return;

        string path = GetCurrentRecordingPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("还没有录音");
            return;
        }

        var clip = WavFileUtil.LoadClip(path);
        if (clip == null)
        {
            SetStatus("录音文件无法播放");
            return;
        }

        EnsureAudio();
        _audio.Stop();
        _audio.clip = clip;
        _audio.Play();
        SetStatus("播放中…");
        StartCoroutine(WaitPlaybackDone(clip.length));
    }

    public void OnRerecordClicked()
    {
        if (_recording)
            StopRecordingAndSave();

        string path = GetCurrentRecordingPath();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }

        if (_pages != null && _pageIndex >= 0 && _pageIndex < _pages.Length && _pages[_pageIndex] != null)
            _pages[_pageIndex].userRecordingFile = "";

        if (!string.IsNullOrWhiteSpace(_saveId))
        {
            var save = CompletedStoryStore.LoadSave(_saveId);
            if (save?.pages != null && _pageIndex >= 0 && _pageIndex < save.pages.Length && save.pages[_pageIndex] != null)
            {
                save.pages[_pageIndex].userRecordingFile = "";
                try
                {
                    File.WriteAllText(
                        Path.Combine(CompletedStoryStore.GetSaveDirectory(_saveId), "story.json"),
                        JsonUtility.ToJson(save, true));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CompletedStoryPageVoiceRecorder] 更新 story.json 失败: {ex.Message}");
                }
            }
        }

        SetStatus("已清除，请重新录音");
        RefreshUi();
        OnRecordClicked();
    }

    IEnumerator AutoStopRecordingAfterTimeout()
    {
        yield return new WaitForSeconds(MaxRecordSeconds);
        if (_recording)
            StopRecordingAndSave();
        _recordRoutine = null;
    }

    IEnumerator WaitPlaybackDone(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, seconds));
        if (_audio != null && !_audio.isPlaying)
            RefreshUi();
    }

    void StopRecordingAndSave()
    {
        if (!_recording)
            return;

        _recording = false;
        if (_recordRoutine != null)
        {
            StopCoroutine(_recordRoutine);
            _recordRoutine = null;
        }

        if (string.IsNullOrEmpty(_micDevice) || _micClip == null)
        {
            RefreshUi();
            return;
        }

        int pos = Microphone.GetPosition(_micDevice);
        Microphone.End(_micDevice);

        if (pos <= 0)
        {
            SetStatus("录音太短，请重试");
            RefreshUi();
            return;
        }

        var samples = new float[pos * _micClip.channels];
        _micClip.GetData(samples, 0);
        float[] mono;
        if (_micClip.channels > 1)
        {
            mono = new float[pos];
            for (int i = 0; i < pos; i++)
            {
                float sum = 0f;
                for (int c = 0; c < _micClip.channels; c++)
                    sum += samples[i * _micClip.channels + c];
                mono[i] = sum / _micClip.channels;
            }
        }
        else
        {
            mono = new float[pos];
            Array.Copy(samples, mono, pos);
        }

        Destroy(_micClip);
        _micClip = null;

        try
        {
            byte[] wav = PcmFloatWavEncoder.EncodeMono16(mono, SampleRate);
            if (!CompletedStoryStore.SavePageUserRecording(_saveId, _pageIndex, wav))
            {
                SetStatus("保存录音失败");
                RefreshUi();
                return;
            }

            var save = CompletedStoryStore.LoadSave(_saveId);
            if (save?.pages != null && _pageIndex >= 0 && _pageIndex < save.pages.Length)
                _pages[_pageIndex] = save.pages[_pageIndex];

            SetStatus("朗读已保存");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CompletedStoryPageVoiceRecorder] 保存失败: {ex.Message}");
            SetStatus("保存录音失败");
        }

        RefreshUi();
    }

    string GetCurrentRecordingPath()
    {
        if (_pages == null || _pageIndex < 0 || _pageIndex >= _pages.Length)
            return null;
        return CompletedStoryStore.GetPageRecordingPath(_saveId, _pages[_pageIndex]);
    }

    void EnsureAudio()
    {
        if (_audio != null)
            return;
        _audio = gameObject.GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
    }

    void UpdateRecordButtonLabel()
    {
        if (_recordButton == null)
            return;
        var label = _recordButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = _recording ? "停止" : "录音";
    }

    void SetStatus(string text)
    {
        if (_statusLabel != null)
            _statusLabel.text = text ?? "";
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

    void OnDisable()
    {
        if (_recording)
            StopRecordingAndSave();
        _audio?.Stop();
    }
}
