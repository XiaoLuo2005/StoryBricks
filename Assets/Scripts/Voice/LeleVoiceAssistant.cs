using System;

/// <summary>语音助手「乐乐」的唤醒词与文案。</summary>
public static class LeleVoiceAssistant
{
    public const string DisplayName = "乐乐";
    public const string WakePhrase = "你好乐乐";
    public const string WakeHint = "说「你好乐乐」唤醒乐乐";
    public const string ListeningHint = "乐乐在听你说话…";
    public const string SpeakingHint = "你在说话…";
    public const string WakeAcknowledgement = "我在呢！请说吧";

    public static bool ContainsWakeWord(string transcript)
    {
        var n = Normalize(transcript);
        if (string.IsNullOrEmpty(n))
            return false;

        if (n.Contains("你好乐乐") || n.Contains("你好勒勒") || n.Contains("你好lele"))
            return true;

        if (n.Contains("乐乐") && (n.Contains("你好") || n.Contains("嗨") || n.Contains("hello")))
            return true;

        return false;
    }

    public static string StripWakePrefix(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return "";

        var t = transcript.Trim();
        string[] prefixes =
        {
            "你好乐乐，", "你好乐乐,", "你好乐乐 ", "你好乐乐",
            "你好，乐乐", "你好 乐乐", "嗨乐乐", "嗨，乐乐", "嗨 乐乐",
        };

        foreach (var prefix in prefixes)
        {
            if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return t.Substring(prefix.Length).TrimStart('，', ',', ' ', '。', '.', '！', '!');
        }

        int idx = t.IndexOf("你好乐乐", StringComparison.Ordinal);
        if (idx >= 0)
        {
            var rest = t.Remove(idx, "你好乐乐".Length);
            return rest.TrimStart('，', ',', ' ', '。', '.', '！', '!');
        }

        return t;
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text.Trim()
            .Replace(" ", "")
            .Replace("，", "")
            .Replace(",", "")
            .Replace("。", "")
            .Replace("！", "")
            .Replace("!", "")
            .Replace("？", "")
            .Replace("?", "")
            .ToLowerInvariant();
    }
}
