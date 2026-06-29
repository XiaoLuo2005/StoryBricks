using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

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
        public string generatedImageUrl = "";
        public string panoramaImageUrl = "";
        [TextArea(2, 8)]
        public string generationPrompt = "";
    }

    static string _storyId = "";
    static string _storyTitle = "";
    static int _currentPageIndex;
    static string _nextPageSceneHint = "";
    static Texture2D _anchorPageTexture;
    static readonly List<PageRecord> _completedPages = new List<PageRecord>();
    static readonly List<Texture2D> _pageTextures = new List<Texture2D>();
    static readonly List<Texture2D> _pagePanoramaTextures = new List<Texture2D>();

    public static string StoryId => _storyId;
    public static string StoryTitle => _storyTitle;
    public static int CurrentPageIndex => _currentPageIndex;
    public static IReadOnlyList<PageRecord> CompletedPages => _completedPages;
    public static Texture2D AnchorPageTexture => _anchorPageTexture;

    public static bool HasActiveSession => !string.IsNullOrWhiteSpace(_storyId);

    /// <summary>上一页对话推断的下一页情境补充（故事分支）。</summary>
    public static string NextPageSceneHint => _nextPageSceneHint ?? "";

    public static void SetNextPageSceneHint(string hint)
    {
        _nextPageSceneHint = hint?.Trim() ?? "";
    }

    public static void ConsumeNextPageSceneHint()
    {
        _nextPageSceneHint = "";
    }

    /// <summary>到目前为止的完整故事叙述（供页末小结）。</summary>
    public static string BuildStorySoFarNarrative()
    {
        return BuildPreviousPagesSummary();
    }

    public static void BeginSession(string storyId, string storyTitle)
    {
        _storyId = storyId ?? "";
        _storyTitle = storyTitle ?? "";
        _currentPageIndex = 0;
        _nextPageSceneHint = "";
        _completedPages.Clear();
        ClearPageTextures();
        ClearAnchorPageTexture();
    }

    public static void SetCurrentPageIndex(int index)
    {
        _currentPageIndex = Math.Max(0, index);
    }

    public static void RecordCompletedPage(PageRecord record, Texture2D pageTexture, int pageIndex, Texture2D panoramaTexture = null)
    {
        if (record == null || pageIndex < 0)
            return;

        if (_completedPages.Count > pageIndex)
        {
            DestroyPageTextureAt(pageIndex);
            DestroyPanoramaTextureAt(pageIndex);
            _completedPages[pageIndex] = record;
            _pageTextures[pageIndex] = DuplicatePageTexture(pageTexture);
            _pagePanoramaTextures[pageIndex] = DuplicatePageTexture(panoramaTexture);
            return;
        }

        if (_completedPages.Count == pageIndex)
        {
            _completedPages.Add(record);
            _pageTextures.Add(DuplicatePageTexture(pageTexture));
            _pagePanoramaTextures.Add(DuplicatePageTexture(panoramaTexture));
        }
    }

    public static Texture2D GetPagePanoramaTexture(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _pagePanoramaTextures.Count)
            return null;
        return _pagePanoramaTextures[pageIndex];
    }

    public static Texture2D GetPageTexture(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _pageTextures.Count)
            return null;
        return _pageTextures[pageIndex];
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

    /// <summary>P1 成图，供 P2/P3 img2img 锚定画风与角色。</summary>
    public static void SetAnchorPageTexture(Texture2D texture)
    {
        ClearAnchorPageTexture();
        if (texture == null)
            return;
        _anchorPageTexture = StoryImageUtil.DuplicateTexture(texture);
    }

    public static void ClearAnchorPageTexture()
    {
        if (_anchorPageTexture != null)
        {
            UnityEngine.Object.Destroy(_anchorPageTexture);
            _anchorPageTexture = null;
        }
    }

    public static void Clear()
    {
        _storyId = "";
        _storyTitle = "";
        _currentPageIndex = 0;
        _nextPageSceneHint = "";
        _completedPages.Clear();
        ClearPageTextures();
        ClearAnchorPageTexture();
    }

    static Texture2D DuplicatePageTexture(Texture2D pageTexture)
    {
        if (pageTexture == null)
            return null;
        return StoryImageUtil.DuplicateTexture(pageTexture);
    }

    static void DestroyPageTextureAt(int index)
    {
        if (index < 0 || index >= _pageTextures.Count)
            return;
        if (_pageTextures[index] != null)
        {
            UnityEngine.Object.Destroy(_pageTextures[index]);
            _pageTextures[index] = null;
        }
    }

    static void DestroyPanoramaTextureAt(int index)
    {
        if (index < 0 || index >= _pagePanoramaTextures.Count)
            return;
        if (_pagePanoramaTextures[index] != null)
        {
            UnityEngine.Object.Destroy(_pagePanoramaTextures[index]);
            _pagePanoramaTextures[index] = null;
        }
    }

    static void ClearPageTextures()
    {
        for (int i = 0; i < _pageTextures.Count; i++)
        {
            if (_pageTextures[i] != null)
                UnityEngine.Object.Destroy(_pageTextures[i]);
        }
        _pageTextures.Clear();

        for (int i = 0; i < _pagePanoramaTextures.Count; i++)
        {
            if (_pagePanoramaTextures[i] != null)
                UnityEngine.Object.Destroy(_pagePanoramaTextures[i]);
        }
        _pagePanoramaTextures.Clear();
    }
}
