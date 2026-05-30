using System;
using UnityEngine;

public static class StorySelectionContext
{
    public static string StoryId { get; private set; } = "";
    public static string Title { get; private set; } = "";
    public static string Synopsis { get; private set; } = "";
    public static string StoryWorksSceneName { get; private set; } = "";
    public static Sprite Cover { get; private set; }
    public static Sprite[] ProloguePages { get; private set; }
    public static BrickPortfolioRoot.BrickWorkItem[] Works { get; private set; }

    public static bool HasSelection => !string.IsNullOrWhiteSpace(StoryId);

    public static bool HasStoryWorks =>
        Works != null && Works.Length > 0 && !string.IsNullOrWhiteSpace(StoryWorksSceneName);

    public static void SetFromStory(StoryDefinition def)
    {
        if (def == null)
        {
            Clear();
            return;
        }

        StoryId = def.storyId ?? "";
        Title = def.title ?? "";
        Synopsis = def.synopsisText ?? "";
        Cover = def.thumbnail;
        ProloguePages = def.prologuePages != null && def.prologuePages.Length > 0 ? def.prologuePages : null;
        StoryWorksSceneName = string.IsNullOrWhiteSpace(def.storyWorksSceneName)
            ? StoryFlowScenes.StoryWorks
            : def.storyWorksSceneName.Trim();
        Works = ConvertWorks(def.works);
    }

    public static string ResolvePortfolioReturnScene(string fallback)
    {
        if (HasStoryWorks)
            return StoryWorksSceneName.Trim();
        return string.IsNullOrWhiteSpace(fallback) ? StoryFlowScenes.BrickLibrary : fallback.Trim();
    }

    static BrickPortfolioRoot.BrickWorkItem[] ConvertWorks(StoryDefinition.StoryBrickWorkEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
            return Array.Empty<BrickPortfolioRoot.BrickWorkItem>();

        var list = new System.Collections.Generic.List<BrickPortfolioRoot.BrickWorkItem>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.tutorialSceneName))
                continue;
            list.Add(new BrickPortfolioRoot.BrickWorkItem
            {
                storyId = string.IsNullOrWhiteSpace(e.workId) ? e.title : e.workId,
                title = e.title,
                tutorialSceneName = e.tutorialSceneName.Trim(),
                thumbnail = e.thumbnail,
            });
        }
        return list.ToArray();
    }

    static void Clear()
    {
        StoryId = "";
        Title = "";
        Synopsis = "";
        StoryWorksSceneName = "";
        Cover = null;
        ProloguePages = null;
        Works = Array.Empty<BrickPortfolioRoot.BrickWorkItem>();
    }
}
