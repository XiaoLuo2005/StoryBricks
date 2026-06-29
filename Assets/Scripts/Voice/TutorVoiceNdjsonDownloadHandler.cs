using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Parses NDJSON lines from POST /api/tutor/voice-stream while the request is in flight.
/// </summary>
public sealed class TutorVoiceNdjsonDownloadHandler : DownloadHandlerScript
{
    readonly ConcurrentQueue<TutorVoiceStreamEvent> _events = new ConcurrentQueue<TutorVoiceStreamEvent>();
    readonly StringBuilder _lineBuffer = new StringBuilder(4096);

    public TutorVoiceNdjsonDownloadHandler() : base(new byte[65536])
    {
    }

    public void DrainEvents(Action<TutorVoiceStreamEvent> onEvent)
    {
        while (_events.TryDequeue(out var evt))
            onEvent?.Invoke(evt);
    }

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0)
            return true;

        _lineBuffer.Append(Encoding.UTF8.GetString(data, 0, dataLength));
        FlushCompleteLines();
        return true;
    }

    protected override void CompleteContent()
    {
        FlushCompleteLines(flushRemainder: true);
    }

    void FlushCompleteLines(bool flushRemainder = false)
    {
        var text = _lineBuffer.ToString();
        if (text.Length == 0)
            return;

        var consumed = 0;
        while (consumed < text.Length)
        {
            var newline = text.IndexOf('\n', consumed);
            if (newline < 0)
                break;

            var line = text.Substring(consumed, newline - consumed).Trim();
            consumed = newline + 1;
            if (line.Length > 0)
                TryEnqueue(line);
        }

        _lineBuffer.Clear();
        if (consumed < text.Length)
        {
            var remainder = text.Substring(consumed).Trim();
            if (flushRemainder && remainder.Length > 0)
                TryEnqueue(remainder);
            else if (remainder.Length > 0)
                _lineBuffer.Append(remainder);
        }
    }

    void TryEnqueue(string line)
    {
        try
        {
            var evt = JsonUtility.FromJson<TutorVoiceStreamEvent>(line);
            if (evt != null && !string.IsNullOrEmpty(evt.stage))
                _events.Enqueue(evt);
        }
        catch (Exception)
        {
            // Ignore malformed partial lines; streaming may split JSON across chunks.
        }
    }
}

[Serializable]
public class TutorVoiceStreamEvent
{
    public string stage;
    public string transcript;
    public string reply;
    public string audioBase64;
    public string audioFormat;
    public string error;
    public int ms;
}
