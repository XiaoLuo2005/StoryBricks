using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 识别完成后分析语音提问缺口：各角色行为 + 可选的固定「还想加什么」文案。
/// 多角色相对站位由 <see cref="BuildAutoPlacementDescription"/> 根据 ArUco 像素坐标自动推算，不再口头提问。
/// </summary>
public static class StoryCreationGapAnalyzer
{
    public enum GapKind
    {
        CharacterBehavior,
        CharacterPosition,
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

    /// <summary>根据当前帧 ArUco 中心像素坐标，推算多角色在镜头里的相对站位描述。</summary>
    public static string BuildAutoPlacementDescription(
        StoryDefinition.StoryPageDefinition page,
        IReadOnlyList<ArUcoDetector.MarkerData> markers,
        StoryDefinition.CharacterReferenceEntry[] characterCatalog,
        StoryMarkerTaxonomy taxonomy)
    {
        if (markers == null || markers.Count == 0)
            return "";

        var detectedSet = new HashSet<int>();
        var detectedCharacters = new List<int>();
        foreach (var m in markers)
        {
            detectedSet.Add(m.id);
            if (taxonomy.IsCharacter(m.id))
                detectedCharacters.Add(m.id);
        }

        detectedCharacters.Sort();
        var characterIds = ResolveCharactersToAsk(page, detectedCharacters, detectedSet);
        if (characterIds.Count < 2)
            return "";

        return DescribeRelativePlacement(characterIds, markers, characterCatalog);
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
            return $"小朋友，{roleName}在这页故事里想做什么呢？{scene} 快来告诉{LeleVoiceAssistant.DisplayName}吧！";
        return $"小朋友，{roleName}现在想做什么呢？快来告诉{LeleVoiceAssistant.DisplayName}吧！";
    }

    static string DescribeRelativePlacement(
        List<int> characterIds,
        IReadOnlyList<ArUcoDetector.MarkerData> markers,
        StoryDefinition.CharacterReferenceEntry[] catalog)
    {
        if (markers == null || characterIds == null || characterIds.Count < 2)
            return "";

        Vector2? posA = FindMarkerPixel(characterIds[0], markers);
        Vector2? posB = FindMarkerPixel(characterIds[1], markers);
        if (!posA.HasValue || !posB.HasValue)
            return "";

        string nameA = ResolveRoleName(characterIds[0], catalog);
        string nameB = ResolveRoleName(characterIds[1], catalog);
        float dx = posB.Value.x - posA.Value.x;
        float dy = posB.Value.y - posA.Value.y;

        if (Mathf.Abs(dx) > Mathf.Abs(dy) * 0.6f)
        {
            if (dx > 0)
                return $"{nameA}在左边，{nameB}在右边";
            return $"{nameA}在右边，{nameB}在左边";
        }

        if (dy > 0)
            return $"{nameA}离镜头近一些，{nameB}在后面";
        return $"{nameB}离镜头近一些，{nameA}在后面";
    }

    static Vector2? FindMarkerPixel(int id, IReadOnlyList<ArUcoDetector.MarkerData> markers)
    {
        foreach (var m in markers)
        {
            if (m.id == id)
                return m.pixelPosition;
        }
        return null;
    }
}
