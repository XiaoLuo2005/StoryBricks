using System.Collections.Generic;

/// <summary>
/// 识别完成后分析语音提问缺口：各角色行为 + 可选的固定「还想加什么」文案。
/// </summary>
public static class StoryCreationGapAnalyzer
{
    public enum GapKind
    {
        CharacterBehavior,
        OptionalStoryElement,
    }

    public struct Gap
    {
        public GapKind kind;
        public int characterMarkerId;
        public string roleName;
        public string fallbackQuestion;
    }

    public static List<Gap> Analyze(
        StoryDefinition.StoryPageDefinition page,
        IReadOnlyList<ArUcoDetector.MarkerData> markers,
        StoryDefinition.CharacterReferenceEntry[] characterCatalog,
        StoryMarkerTaxonomy taxonomy)
    {
        var gaps = new List<Gap>();
        if (markers == null || markers.Count == 0)
            return gaps;

        var detectedSet = new HashSet<int>();
        var detectedCharacters = new List<int>();

        foreach (var m in markers)
        {
            detectedSet.Add(m.id);
            if (taxonomy.IsCharacter(m.id))
                detectedCharacters.Add(m.id);
        }

        detectedCharacters.Sort();
        var askCharacterIds = ResolveCharactersToAsk(page, detectedCharacters, detectedSet);

        foreach (int characterId in askCharacterIds)
        {
            string role = ResolveRoleName(characterId, characterCatalog);
            gaps.Add(new Gap
            {
                kind = GapKind.CharacterBehavior,
                characterMarkerId = characterId,
                roleName = role,
                fallbackQuestion = BuildCharacterBehaviorFallback(role, page),
            });
        }

        if (!string.IsNullOrWhiteSpace(page?.optionalElementQuestion))
        {
            gaps.Add(new Gap
            {
                kind = GapKind.OptionalStoryElement,
                fallbackQuestion = page.optionalElementQuestion.Trim(),
            });
        }

        return gaps;
    }

    static List<int> ResolveCharactersToAsk(
        StoryDefinition.StoryPageDefinition page,
        List<int> detectedCharacters,
        HashSet<int> detectedSet)
    {
        var result = new List<int>();
        if (page?.requiredCharacterIds != null && page.requiredCharacterIds.Length > 0)
        {
            foreach (int id in page.requiredCharacterIds)
            {
                if (detectedSet.Contains(id))
                    result.Add(id);
            }
            return result;
        }

        return new List<int>(detectedCharacters);
    }

    static string ResolveRoleName(int markerId, StoryDefinition.CharacterReferenceEntry[] catalog)
    {
        if (catalog != null)
        {
            foreach (var entry in catalog)
            {
                if (entry != null && entry.markerId == markerId &&
                    !string.IsNullOrWhiteSpace(entry.roleName))
                    return entry.roleName.Trim();
            }
        }
        return $"角色{markerId}";
    }

    static string BuildCharacterBehaviorFallback(
        string roleName,
        StoryDefinition.StoryPageDefinition page)
    {
        string scene = page?.sceneGuideText?.Trim() ?? "";
        if (!string.IsNullOrEmpty(scene))
            return $"小朋友，{roleName}在这页故事里想做什么呢？{scene} 快来告诉老师吧！";
        return $"小朋友，{roleName}现在想做什么呢？快来告诉老师吧！";
    }
}
