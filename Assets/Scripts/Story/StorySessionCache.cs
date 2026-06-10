using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 单次故事创作会话的跨页缓存：供 AI 提问、提示词拼接、下一页剧情延续使用。
/// </summary>
public static class StorySessionCache
{
    [Serializable]
    public class PageRecord
    {
        public string pageId = "";
        public string pageTitle = "";
        public string sceneGuideText = "";
        public string voiceGuideText = "";
        public string userVoiceAnswer = "";
        public string generatedStoryText = "";
        public string generatedImageNote = "";
    }

    static string _storyId = "";
    static string _storyTitle = "";
    static int _currentPageIndex;
    static readonly List<PageRecord> _completedPages = new List<PageRecord>();

    public static string StoryId => _storyId;
    public static string StoryTitle => _storyTitle;
    public static int CurrentPageIndex => _currentPageIndex;
    public static IReadOnlyList<PageRecord> CompletedPages => _completedPages;

    public static bool HasActiveSession => !string.IsNullOrWhiteSpace(_storyId);

    public static void BeginSession(string storyId, string storyTitle)
    {
        _storyId = storyId ?? "";
        _storyTitle = storyTitle ?? "";
        _currentPageIndex = 0;
        _completedPages.Clear();
    }

    public static void SetCurrentPageIndex(int index)
    {
        _currentPageIndex = Math.Max(0, index);
    }

    public static void RecordCompletedPage(PageRecord record)
    {
        if (record == null)
            return;
        _completedPages.Add(record);
    }

    public static PageRecord GetLastCompletedPage()
    {
        if (_completedPages.Count == 0)
            return null;
        return _completedPages[_completedPages.Count - 1];
    }

    /// <summary>拼接上一页及更早页的剧情摘要，供 AI 提问与 Prompt 使用。</summary>
    public static string BuildPreviousPagesSummary()
    {
        if (_completedPages.Count == 0)
            return "";

        var sb = new StringBuilder();
        for (int i = 0; i < _completedPages.Count; i++)
        {
            var p = _completedPages[i];
            if (p == null)
                continue;
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append($"【{p.pageTitle}】");
            if (!string.IsNullOrWhiteSpace(p.generatedStoryText))
                sb.Append(p.generatedStoryText.Trim());
            else if (!string.IsNullOrWhiteSpace(p.userVoiceAnswer))
                sb.Append($"儿童补充：{p.userVoiceAnswer.Trim()}");
            else
                sb.Append(p.sceneGuideText?.Trim() ?? "");
        }
        return sb.ToString();
    }

    public static void Clear()
    {
        _storyId = "";
        _storyTitle = "";
        _currentPageIndex = 0;
        _completedPages.Clear();
    }
}
