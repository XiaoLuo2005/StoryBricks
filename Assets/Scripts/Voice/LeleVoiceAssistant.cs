using System;
using UnityEngine;

/// <summary>语音助手「乐乐」的唤醒词与文案。</summary>
public static class LeleVoiceAssistant
{
    public const string DisplayName = "乐乐";
    public const string WakePhrase = "你好乐乐";
    public const string WakeHint = "说「你好乐乐」唤醒乐乐";
    public const string ListeningHint = "乐乐在听你说话…";
    public const string ListeningLiveHint = "正在听…";
    public const string TranscribingHint = "识别中…";
    public const string ThinkingHint = "想一想…";
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

    /// <summary>识别结果去掉唤醒词后是否没有实质内容（避免把「你好乐乐」回声发给大模型）。</summary>
    public static bool IsWakeOnlyTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return true;

        var remainder = StripWakePrefix(transcript);
        if (!string.IsNullOrWhiteSpace(remainder))
            return false;

        return ContainsWakeWord(transcript);
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

    /// <summary>识别结果是否像扬声器里漏进的乐乐上一句（避免显示成「你：…」）。</summary>
    public static bool LooksLikeEchoOf(string transcript, string referenceText)
    {
        if (string.IsNullOrWhiteSpace(transcript) || string.IsNullOrWhiteSpace(referenceText))
            return false;

        var a = Normalize(transcript);
        var b = Normalize(referenceText);
        if (a.Length < 4 || b.Length < 4)
            return false;

        if (a == b)
            return true;

        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;
        if (shorter.Length >= 5 && longer.Contains(shorter))
            return true;

        int prefixLen = Math.Min(Math.Min(a.Length, b.Length), 12);
        if (prefixLen >= 5 && a.Substring(0, prefixLen) == b.Substring(0, prefixLen))
            return true;

        int lcsLen = LongestCommonSubstringLength(a, b);
        if (lcsLen >= 8 && lcsLen >= Math.Min(a.Length, b.Length) * 0.32f)
            return true;

        if (SimilarityRatio(a, b) >= 0.48f)
            return true;

        return SharedSegmentRatio(a, b) >= 0.42f;
    }

    public static bool LooksLikeEchoOfAny(string transcript, System.Collections.Generic.IReadOnlyList<string> references)
    {
        if (string.IsNullOrWhiteSpace(transcript) || references == null)
            return false;

        foreach (var reference in references)
        {
            if (LooksLikeEchoOf(transcript, reference))
                return true;
        }

        return false;
    }

    static float SimilarityRatio(string a, string b)
    {
        int lcs = LongestCommonSubstringLength(a, b);
        if (lcs == 0)
            return 0f;
        return (2f * lcs) / (a.Length + b.Length);
    }

    /// <summary>两段文本里，较短段有多少字符能在较长段里按顺序找到。</summary>
    static float SharedSegmentRatio(string a, string b)
    {
        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;
        if (shorter.Length < 6)
            return 0f;

        int matched = 0;
        int start = 0;
        while (start < shorter.Length)
        {
            int hit = longer.IndexOf(shorter[start], StringComparison.Ordinal);
            if (hit < 0)
                break;

            int len = 1;
            while (start + len < shorter.Length &&
                   hit + len < longer.Length &&
                   shorter[start + len] == longer[hit + len])
            {
                len++;
            }

            matched += len;
            start += len;
        }

        return (float)matched / shorter.Length;
    }

    static int LongestCommonSubstringLength(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        int best = 0;
        var rows = a.Length + 1;
        var cols = b.Length + 1;
        var dp = new int[rows, cols];
        for (int i = 1; i < rows; i++)
        {
            for (int j = 1; j < cols; j++)
            {
                if (a[i - 1] != b[j - 1])
                    continue;

                dp[i, j] = dp[i - 1, j - 1] + 1;
                if (dp[i, j] > best)
                    best = dp[i, j];
            }
        }

        return best;
    }
}
