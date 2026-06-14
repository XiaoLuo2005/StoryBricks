using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 分页创作：ArUco 校验、角色参考图收集、Prompt 组装。
/// </summary>
public static class StoryPageGenerationPipeline
{
    public const int MaxReferenceImages = 4;

    public struct ValidationResult
    {
        public bool ok;
        public string message;
        public List<int> detectedIds;
        public List<int> missingIds;
    }

    public struct ReferenceBundle
    {
        public Texture2D[] textures;
        public bool hasAnchor;
        public int characterCount;
        public List<Texture2D> temporaryTextures;
    }

    public static List<int> CollectDetectedMarkerIds(ArUcoDetector detector)
    {
        var ids = new List<int>();
        if (detector?.DetectedMarkers == null)
            return ids;

        var seen = new HashSet<int>();
        foreach (var marker in detector.DetectedMarkers)
        {
            if (seen.Add(marker.id))
                ids.Add(marker.id);
        }
        ids.Sort();
        return ids;
    }

    public static ValidationResult ValidateRequiredCharacters(
        StoryDefinition.StoryPageDefinition page,
        IReadOnlyList<int> detectedIds)
    {
        var result = new ValidationResult
        {
            ok = true,
            detectedIds = detectedIds != null ? new List<int>(detectedIds) : new List<int>(),
            missingIds = new List<int>(),
        };

        if (page?.requiredCharacterIds == null || page.requiredCharacterIds.Length == 0)
        {
            if (result.detectedIds.Count == 0)
            {
                result.ok = false;
                result.message = "未识别到积木，请摆放角色后重试。";
            }
            return result;
        }

        var detectedSet = new HashSet<int>(result.detectedIds);
        foreach (int requiredId in page.requiredCharacterIds)
        {
            if (!detectedSet.Contains(requiredId))
                result.missingIds.Add(requiredId);
        }

        if (result.missingIds.Count > 0)
        {
            result.ok = false;
            result.message = $"缺少角色积木（ArUco ID：{string.Join("、", result.missingIds)}），请补全后重试。";
        }
        else if (result.detectedIds.Count == 0)
        {
            result.ok = false;
            result.message = "未识别到积木，请摆放角色后重试。";
        }

        return result;
    }

    /// <summary>
    /// 按 detectedIds 顺序收集角色标准图；P2+ 可追加 P1 锚图（占 1 个 slot）。
    /// </summary>
    public static ReferenceBundle CollectCharacterReferenceTextures(
        IReadOnlyList<int> detectedIds,
        StoryDefinition.CharacterReferenceEntry[] catalog,
        Texture2D anchorTexture)
    {
        var textures = new List<Texture2D>();
        var temporaryTextures = new List<Texture2D>();
        int characterCount = 0;

        if (detectedIds != null && catalog != null)
        {
            foreach (int id in detectedIds)
            {
                if (textures.Count >= MaxReferenceImages)
                    break;

                var entry = FindCharacterEntry(catalog, id);
                if (entry?.referenceSprite == null)
                    continue;

                var tex = StoryImageUtil.SpriteToTexture(entry.referenceSprite);
                if (tex == null)
                    continue;

                textures.Add(tex);
                characterCount++;
                if (entry.referenceSprite.texture != tex)
                    temporaryTextures.Add(tex);
            }
        }

        bool hasAnchor = false;
        if (anchorTexture != null && textures.Count < MaxReferenceImages)
        {
            textures.Add(anchorTexture);
            hasAnchor = true;
        }

        return new ReferenceBundle
        {
            textures = textures.ToArray(),
            hasAnchor = hasAnchor,
            characterCount = characterCount,
            temporaryTextures = temporaryTextures,
        };
    }

    public static void ReleaseTemporaryTextures(ReferenceBundle bundle)
    {
        if (bundle.temporaryTextures == null)
            return;
        foreach (var tex in bundle.temporaryTextures)
        {
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
        }
        bundle.temporaryTextures.Clear();
    }

    public static string BuildGenerationPrompt(
        StoryDefinition.StoryPageDefinition page,
        StoryDefinition.CharacterReferenceEntry[] catalog,
        IReadOnlyList<int> detectedIds,
        string stylePromptPrefix,
        ReferenceBundle references)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(stylePromptPrefix))
        {
            sb.Append(stylePromptPrefix.Trim());
            sb.Append('。');
        }

        sb.Append("参考");
        var roleParts = new List<string>();
        if (detectedIds != null && catalog != null)
        {
            foreach (int id in detectedIds)
            {
                var entry = FindCharacterEntry(catalog, id);
                if (entry?.referenceSprite == null)
                    continue;
                string role = string.IsNullOrWhiteSpace(entry.roleName) ? $"角色{id}" : entry.roleName.Trim();
                roleParts.Add($"图{roleParts.Count + 1}的{role}外貌");
            }
        }

        if (roleParts.Count > 0)
            sb.Append(string.Join("、", roleParts));

        if (references.hasAnchor)
        {
            if (roleParts.Count > 0)
                sb.Append('、');
            sb.Append($"图{roleParts.Count + 1}的绘本画风");
        }

        sb.Append("，生成儿童绘本插画。");

        if (page != null)
        {
            if (!string.IsNullOrWhiteSpace(page.pageTitle))
                sb.Append($"本页场景：{page.pageTitle.Trim()}。");
            if (!string.IsNullOrWhiteSpace(page.sceneGuideText))
                sb.Append(page.sceneGuideText.Trim());
            if (!string.IsNullOrWhiteSpace(page.sceneGuideText) &&
                !page.sceneGuideText.TrimEnd().EndsWith("。") &&
                !page.sceneGuideText.TrimEnd().EndsWith("！") &&
                !page.sceneGuideText.TrimEnd().EndsWith("?") &&
                !page.sceneGuideText.TrimEnd().EndsWith("？"))
                sb.Append('。');
        }

        string previous = StorySessionCache.BuildPreviousPagesSummary();
        if (!string.IsNullOrWhiteSpace(previous))
            sb.Append("前情：").Append(previous.Trim()).Append('。');

        sb.Append("角色外貌必须与参考图一致，柔和水彩绘本风格，横版构图，无文字无水印。");
        return sb.ToString();
    }

    static StoryDefinition.CharacterReferenceEntry FindCharacterEntry(
        StoryDefinition.CharacterReferenceEntry[] catalog,
        int markerId)
    {
        if (catalog == null)
            return null;
        foreach (var entry in catalog)
        {
            if (entry != null && entry.markerId == markerId)
                return entry;
        }
        return null;
    }
}
