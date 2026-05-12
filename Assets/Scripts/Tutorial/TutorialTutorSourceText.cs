using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 从 TextAsset 解析发给 AI 的 stepHints / stepTutorDetails，便于用 .txt 维护后覆盖 Inspector 数组。
/// </summary>
public static class TutorialTutorSourceText
{
    static readonly string[] DetailKeys = { "GOAL:", "PARTS:", "ACTIONS:", "PITFALLS:" };

    /// <summary>非空行按顺序对应各步；# 开头为注释。行数不足补空串。</summary>
    public static string[] ParseStepHintsLines(string raw, int stepCount)
    {
        if (stepCount <= 0 || string.IsNullOrWhiteSpace(raw))
            return null;

        var lines = new List<string>();
        foreach (var line in raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
                continue;
            lines.Add(t);
        }

        var arr = new string[stepCount];
        for (var i = 0; i < stepCount; i++)
            arr[i] = i < lines.Count ? lines[i] : "";
        return arr;
    }

    /// <summary>用 :::STEP::: 分块；每块内 GOAL:/PARTS:/ACTIONS:/PITFALLS: 可多行续写至下一键。</summary>
    public static TutorialStepTutorDetail[] ParseStepDetailBlocks(string raw, int stepCount)
    {
        if (stepCount <= 0 || string.IsNullOrWhiteSpace(raw))
            return null;

        var blocks = raw.Split(new[] { ":::STEP:::" }, StringSplitOptions.None);
        var list = new List<TutorialStepTutorDetail>();
        foreach (var block in blocks)
        {
            var d = ParseOneDetailBlock(block);
            if (d != null)
                list.Add(d);
        }

        var arr = new TutorialStepTutorDetail[stepCount];
        for (var i = 0; i < stepCount; i++)
        {
            if (i < list.Count)
                arr[i] = list[i];
            else
                arr[i] = new TutorialStepTutorDetail();
        }
        return arr;
    }

    static TutorialStepTutorDetail ParseOneDetailBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
            return null;

        var goal = "";
        var parts = "";
        var actions = "";
        var pitfalls = "";
        string currentKey = null;
        var sb = new StringBuilder();

        void Flush()
        {
            if (currentKey == null)
                return;
            var s = sb.ToString().Trim();
            if (currentKey == "GOAL")
                goal = s;
            else if (currentKey == "PARTS")
                parts = s;
            else if (currentKey == "ACTIONS")
                actions = s;
            else if (currentKey == "PITFALLS")
                pitfalls = s;
            sb.Length = 0;
            currentKey = null;
        }

        foreach (var line in block.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            var t = line.TrimEnd();
            var key = StartsWithDetailKey(t, out var rest);
            if (key != null)
            {
                Flush();
                currentKey = key;
                if (!string.IsNullOrEmpty(rest))
                {
                    sb.Append(rest);
                }
            }
            else if (currentKey != null)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(line.TrimEnd());
            }
        }

        Flush();

        if (goal.Length == 0 && parts.Length == 0 && actions.Length == 0 && pitfalls.Length == 0)
            return null;

        return new TutorialStepTutorDetail
        {
            stepGoal = goal,
            partsUsed = parts,
            keyActions = actions,
            pitfalls = pitfalls,
        };
    }

    static string StartsWithDetailKey(string line, out string rest)
    {
        rest = "";
        var trimmed = line.TrimStart();
        foreach (var k in DetailKeys)
        {
            if (!trimmed.StartsWith(k, StringComparison.OrdinalIgnoreCase))
                continue;
            rest = trimmed.Length > k.Length ? trimmed.Substring(k.Length).Trim() : "";
            if (k.StartsWith("GOAL", StringComparison.OrdinalIgnoreCase))
                return "GOAL";
            if (k.StartsWith("PARTS", StringComparison.OrdinalIgnoreCase))
                return "PARTS";
            if (k.StartsWith("ACTIONS", StringComparison.OrdinalIgnoreCase))
                return "ACTIONS";
            if (k.StartsWith("PITFALLS", StringComparison.OrdinalIgnoreCase))
                return "PITFALLS";
        }
        return null;
    }
}
