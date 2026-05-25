using UnityEngine;

public static class StorySelectionContext
{
    public static string StoryId { get; private set; } = "";
    public static string Title { get; private set; } = "";
    public static string Synopsis { get; private set; } = "";
    public static string BuildSceneName { get; private set; } = "";
    public static Sprite Cover { get; private set; }
    public static Sprite[] ProloguePages { get; private set; }

    public static bool HasSelection => !string.IsNullOrWhiteSpace(BuildSceneName);

    public static void Set(string storyId, string title, string synopsis, string buildSceneName, Sprite cover, Sprite[] prologuePages = null)
    {
        StoryId = storyId ?? "";
        Title = title ?? "";
        Synopsis = synopsis ?? "";
        BuildSceneName = buildSceneName ?? "";
        Cover = cover;
        ProloguePages = prologuePages != null && prologuePages.Length > 0 ? prologuePages : null;
    }
}
