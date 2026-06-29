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
            result.message = FormatKidMissingMessage(result.missingIds, null);
        }
        else if (result.detectedIds.Count == 0)
        {
            result.ok = false;
            result.message = "未识别到积木，请摆放角色后重试。";
        }

        return result;
    }

    public static string FormatKidMissingMessage(
        IReadOnlyList<int> missingIds,
        StoryDefinition.CharacterReferenceEntry[] catalog)
    {
        if (missingIds == null || missingIds.Count == 0)
            return "请把积木摆进镜头后再试。";

        var names = new List<string>();
        foreach (int id in missingIds)
        {
            string role = ResolveRoleNameForMarker(id, catalog);
            names.Add(role);
        }

        if (names.Count == 1)
            return $"还差 {names[0]} 的积木，把它放进镜头吧！";
        return $"还差 {string.Join("、", names)}，把积木都摆齐再点确认。";
    }

    static string ResolveRoleNameForMarker(int markerId, StoryDefinition.CharacterReferenceEntry[] catalog)
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
        return $"伙伴{markerId}";
    }

    public static string BuildMandatoryRolesClause(
        IReadOnlyList<int> detectedIds,
        StoryDefinition.CharacterReferenceEntry[] catalog)
    {
        if (detectedIds == null || detectedIds.Count == 0)
            return "";

        var names = new List<string>();
        var taxonomy = StoryMarkerTaxonomy.Default;
        foreach (int id in detectedIds)
        {
            if (!taxonomy.IsCharacter(id))
                continue;
            names.Add(ResolveRoleNameForMarker(id, catalog));
        }

        if (names.Count == 0)
            return "";

        if (names.Count == 1)
            return $"本页画面必须清晰呈现{names[0]}，不得省略或只画背景。";

        return $"本页画面必须同时清晰呈现以下全部角色，缺一不可：{string.Join("、", names)}。";
    }

    public static string EnrichVoiceSupplementWithRequiredRoles(
        string supplement,
        IReadOnlyList<int> detectedIds,
        StoryDefinition.CharacterReferenceEntry[] catalog)
    {
        supplement = supplement?.Trim() ?? "";
        if (detectedIds == null || detectedIds.Count == 0)
            return supplement;

        var taxonomy = StoryMarkerTaxonomy.Default;
        var missing = new List<string>();
        foreach (int id in detectedIds)
        {
            if (!taxonomy.IsCharacter(id))
                continue;
            string role = ResolveRoleNameForMarker(id, catalog);
            if (!string.IsNullOrEmpty(role) && !supplement.Contains(role))
                missing.Add(role);
        }

        if (missing.Count == 0)
            return supplement;

        string note = missing.Count == 1
            ? $"{missing[0]}也在本页镜头里。"
            : $"{string.Join("、", missing)}也都在本页镜头里。";
        return string.IsNullOrEmpty(supplement) ? note : $"{supplement}；{note}";
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
            var taxonomy = StoryMarkerTaxonomy.Default;
            foreach (int id in taxonomy.FilterCharacterIds(detectedIds))
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

    /// <summary>生图前各来源的原始文本，供 AI 整理或本地 fallback 拼接。</summary>
    public struct PromptInputBundle
    {
        public string storyTitle;
        public string stylePromptPrefix;
        public string pageTitle;
        public string sceneGuideText;
        public string previousSummary;
        public string voiceSupplement;
        public bool isContinuationPage;
        /// <summary>如「参考图1的兔子外貌、图2的乌龟外貌，生成儿童绘本插画。」</summary>
        public string referenceImageClause;
        public string detectedRolesDescription;
        /// <summary>抓拍锁定的角色名单，生图时必须全部出现在画面中。</summary>
        public string mandatoryRolesClause;
    }

    public static PromptInputBundle CollectPromptInputs(
        StoryDefinition.StoryPageDefinition page,
        StoryDefinition.CharacterReferenceEntry[] catalog,
        IReadOnlyList<int> detectedIds,
        string stylePromptPrefix,
        ReferenceBundle references,
        string voiceSupplement = "")
    {
        var taxonomy = StoryMarkerTaxonomy.Default;
        var characterIds = taxonomy.FilterCharacterIds(detectedIds);
        var roleNames = new List<string>();

        var sbRef = new StringBuilder();
        sbRef.Append("参考");
        var roleParts = new List<string>();
        if (characterIds != null && catalog != null)
        {
            foreach (int id in characterIds)
            {
                var entry = FindCharacterEntry(catalog, id);
                if (entry?.referenceSprite == null)
                    continue;
                string role = string.IsNullOrWhiteSpace(entry.roleName) ? $"角色{id}" : entry.roleName.Trim();
                roleNames.Add(role);
                roleParts.Add($"图{roleParts.Count + 1}的{role}外貌");
            }
        }

        if (roleParts.Count > 0)
            sbRef.Append(string.Join("、", roleParts));

        if (references.hasAnchor)
        {
            if (roleParts.Count > 0)
                sbRef.Append('、');
            sbRef.Append($"图{roleParts.Count + 1}的绘本画风与色调（仅作风格参考，不得复制其场景构图）");
        }

        sbRef.Append("，生成儿童绘本插画。");

        return new PromptInputBundle
        {
            storyTitle = StorySessionCache.StoryTitle ?? "",
            stylePromptPrefix = stylePromptPrefix ?? "",
            pageTitle = page?.pageTitle ?? "",
            sceneGuideText = page?.sceneGuideText ?? "",
            previousSummary = StorySessionCache.BuildPreviousPagesSummary(),
            voiceSupplement = voiceSupplement ?? "",
            isContinuationPage = StorySessionCache.CompletedPages.Count > 0,
            referenceImageClause = sbRef.ToString(),
            detectedRolesDescription = roleNames.Count > 0 ? string.Join("、", roleNames) : "",
            mandatoryRolesClause = BuildMandatoryRolesClause(detectedIds, catalog),
        };
    }

    /// <summary>AI 整理失败时的本地拼接（与旧版 BuildGenerationPrompt 行为一致）。</summary>
    public static string BuildLocalGenerationPrompt(PromptInputBundle bundle)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(bundle.stylePromptPrefix))
        {
            sb.Append(bundle.stylePromptPrefix.Trim());
            sb.Append('。');
        }

        if (!string.IsNullOrWhiteSpace(bundle.referenceImageClause))
            sb.Append(bundle.referenceImageClause.Trim());

        if (bundle.isContinuationPage)
        {
            sb.Append("这是故事续页，必须绘制与前面页面完全不同的新场景：");
            sb.Append("更换背景环境、角色站位与动作，严禁重复上一页的画面布局。");
        }

        if (!string.IsNullOrWhiteSpace(bundle.pageTitle))
            sb.Append($"本页场景：{bundle.pageTitle.Trim()}。");

        if (!string.IsNullOrWhiteSpace(bundle.sceneGuideText))
        {
            sb.Append(bundle.sceneGuideText.Trim());
            AppendSentenceEndIfNeeded(sb, bundle.sceneGuideText);
        }

        if (!string.IsNullOrWhiteSpace(bundle.previousSummary))
            sb.Append("前情：").Append(bundle.previousSummary.Trim()).Append('。');

        if (!string.IsNullOrWhiteSpace(bundle.voiceSupplement))
            sb.Append("儿童语音补充：").Append(bundle.voiceSupplement.Trim()).Append('。');

        if (!string.IsNullOrWhiteSpace(bundle.mandatoryRolesClause))
            sb.Append(bundle.mandatoryRolesClause.Trim()).Append('。');

        sb.Append(HardConstraintsSuffix);
        return sb.ToString();
    }

    const string HardConstraintsSuffix =
        "镜头里已识别的全部角色都必须出现在画面中，不得只画其中一个或只画背景；角色外貌必须与参考图一致，柔和水彩绘本风格，横版构图；" +
        "画面中禁止任何文字、字母、数字、字幕、标题、水印；" +
        "禁止空白对话框、对白气泡、漫画台词框、speech bubble、caption box 或任何留白文字区域，不得用框遮挡角色或场景元素。";

    /// <summary>将 AI 整理后的场景描述与参考图说明、硬性约束合并为最终生图 Prompt。</summary>
    public static string AssembleFinalPrompt(PromptInputBundle bundle, string aiRefinedSceneText)
    {
        if (string.IsNullOrWhiteSpace(aiRefinedSceneText))
            return BuildLocalGenerationPrompt(bundle);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(bundle.referenceImageClause))
            sb.Append(bundle.referenceImageClause.Trim());

        string scene = StripDuplicateReferencePreface(aiRefinedSceneText, bundle.referenceImageClause);
        sb.Append(scene);
        AppendSentenceEndIfNeeded(sb, scene);
        if (!string.IsNullOrWhiteSpace(bundle.mandatoryRolesClause))
            sb.Append(bundle.mandatoryRolesClause.Trim()).Append('。');
        sb.Append(HardConstraintsSuffix);
        return sb.ToString();
    }

    /// <summary>AI 常会重复输出「参考图…生成儿童绘本插画」，与句首 referenceImageClause 去重。</summary>
    static string StripDuplicateReferencePreface(string aiText, string referenceClause)
    {
        if (string.IsNullOrWhiteSpace(aiText))
            return "";

        var t = aiText.Trim();
        var clause = referenceClause?.Trim() ?? "";
        if (!string.IsNullOrEmpty(clause) &&
            t.StartsWith(clause, System.StringComparison.Ordinal))
        {
            t = t.Substring(clause.Length).TrimStart();
        }

        const string tail = "生成儿童绘本插画。";
        while (t.StartsWith("参考图", System.StringComparison.Ordinal))
        {
            int end = t.IndexOf(tail, System.StringComparison.Ordinal);
            if (end < 0)
                break;
            string next = t.Substring(end + tail.Length).TrimStart();
            if (next.Length >= t.Length)
                break;
            t = next;
        }

        return t;
    }

    static void AppendSentenceEndIfNeeded(StringBuilder sb, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        string t = text.TrimEnd();
        if (t.EndsWith("。") || t.EndsWith("！") || t.EndsWith("?") || t.EndsWith("？"))
            return;
        sb.Append('。');
    }

    public static string BuildGenerationPrompt(
        StoryDefinition.StoryPageDefinition page,
        StoryDefinition.CharacterReferenceEntry[] catalog,
        IReadOnlyList<int> detectedIds,
        string stylePromptPrefix,
        ReferenceBundle references,
        string voiceSupplement = "")
    {
        var bundle = CollectPromptInputs(page, catalog, detectedIds, stylePromptPrefix, references, voiceSupplement);
        return BuildLocalGenerationPrompt(bundle);
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
