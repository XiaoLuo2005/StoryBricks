using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 将一次完整故事（绘本前情 + 创作页成图）保存到 persistentDataPath，并维护索引。
/// </summary>
public static class CompletedStoryStore
{
    const string RootFolderName = "CompletedStories";
    const string IndexFileName = "index.json";

    [Serializable]
    public class CompletedStoryIndexEntry
    {
        public string saveId = "";
        public string storyId = "";
        public string title = "";
        public string savedAtUtc = "";
        public string coverImageFile = "cover.png";
        public int totalPageCount;
    }

    [Serializable]
    public class CompletedStoryIndexFile
    {
        public CompletedStoryIndexEntry[] entries = Array.Empty<CompletedStoryIndexEntry>();
    }

    [Serializable]
    public class CompletedStoryPageFile
    {
        public string pageId = "";
        public string pageTitle = "";
        public string imageFile = "";
        public string userVoiceAnswer = "";
        public string generatedStoryText = "";
        public bool isPrologue;
    }

    [Serializable]
    public class CompletedStorySaveFile
    {
        public string saveId = "";
        public string storyId = "";
        public string title = "";
        public string synopsisText = "";
        public string savedAtUtc = "";
        public CompletedStoryPageFile[] pages = Array.Empty<CompletedStoryPageFile>();
    }

    public static string RootPath => Path.Combine(Application.persistentDataPath, RootFolderName);

    public static string SaveFromSession()
    {
        if (!StorySessionCache.HasActiveSession)
        {
            Debug.LogWarning("[CompletedStoryStore] 无活跃创作会话，跳过保存。");
            return null;
        }

        var pages = StorySessionCache.CompletedPages;
        if (pages == null || pages.Count == 0)
        {
            Debug.LogWarning("[CompletedStoryStore] 会话内无已完成创作页，跳过保存。");
            return null;
        }

        string storyId = StorySessionCache.StoryId;
        string title = StorySessionCache.StoryTitle;
        string synopsis = StorySelectionContext.Synopsis ?? "";
        string saveId = $"{SanitizeFileName(storyId)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        string saveDir = Path.Combine(RootPath, saveId);
        Directory.CreateDirectory(saveDir);

        var pageFiles = new List<CompletedStoryPageFile>();
        int imageIndex = 0;

        var prologue = StorySelectionContext.ProloguePages;
        if (prologue != null)
        {
            for (int i = 0; i < prologue.Length; i++)
            {
                if (prologue[i] == null)
                    continue;
                string fileName = $"prologue_{i:D2}.png";
                SaveSpritePng(prologue[i], Path.Combine(saveDir, fileName));
                pageFiles.Add(new CompletedStoryPageFile
                {
                    pageId = $"prologue_{i}",
                    pageTitle = $"前情 {i + 1}",
                    imageFile = fileName,
                    isPrologue = true,
                });
                imageIndex++;
            }
        }

        string coverFile = "cover.png";
        bool coverSaved = false;

        for (int i = 0; i < pages.Count; i++)
        {
            var record = pages[i];
            if (record == null)
                continue;

            var texture = StorySessionCache.GetPageTexture(i);
            if (texture == null)
                continue;

            string fileName = $"page_{i:D2}.png";
            SaveTexturePng(texture, Path.Combine(saveDir, fileName));
            pageFiles.Add(new CompletedStoryPageFile
            {
                pageId = record.pageId ?? "",
                pageTitle = record.pageTitle ?? "",
                imageFile = fileName,
                userVoiceAnswer = record.userVoiceAnswer ?? "",
                generatedStoryText = record.generatedStoryText ?? "",
                isPrologue = false,
            });

            if (!coverSaved)
            {
                SaveTexturePng(texture, Path.Combine(saveDir, coverFile));
                coverSaved = true;
            }

            imageIndex++;
        }

        if (pageFiles.Count == 0)
        {
            Debug.LogWarning("[CompletedStoryStore] 没有可保存的图片，跳过。");
            try { Directory.Delete(saveDir, true); } catch { /* ignore */ }
            return null;
        }

        if (!coverSaved && prologue != null)
        {
            for (int i = prologue.Length - 1; i >= 0; i--)
            {
                if (prologue[i] == null)
                    continue;
                SaveSpritePng(prologue[i], Path.Combine(saveDir, coverFile));
                coverSaved = true;
                break;
            }
        }

        var saveFile = new CompletedStorySaveFile
        {
            saveId = saveId,
            storyId = storyId,
            title = title,
            synopsisText = synopsis,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            pages = pageFiles.ToArray(),
        };
        File.WriteAllText(Path.Combine(saveDir, "story.json"), JsonUtility.ToJson(saveFile, true));

        UpsertIndex(new CompletedStoryIndexEntry
        {
            saveId = saveId,
            storyId = storyId,
            title = title,
            savedAtUtc = saveFile.savedAtUtc,
            coverImageFile = coverFile,
            totalPageCount = pageFiles.Count,
        });

        Debug.Log($"[CompletedStoryStore] 已保存绘本 → {saveDir}（{pageFiles.Count} 页）");
        return saveId;
    }

    public static CompletedStoryIndexEntry[] LoadIndex()
    {
        EnsureRoot();
        string indexPath = Path.Combine(RootPath, IndexFileName);
        if (!File.Exists(indexPath))
            return Array.Empty<CompletedStoryIndexEntry>();

        try
        {
            var index = JsonUtility.FromJson<CompletedStoryIndexFile>(File.ReadAllText(indexPath));
            return index?.entries ?? Array.Empty<CompletedStoryIndexEntry>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CompletedStoryStore] 读取索引失败: {ex.Message}");
            return Array.Empty<CompletedStoryIndexEntry>();
        }
    }

    public static CompletedStorySaveFile LoadSave(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId))
            return null;

        string path = Path.Combine(RootPath, saveId.Trim(), "story.json");
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonUtility.FromJson<CompletedStorySaveFile>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CompletedStoryStore] 读取绘本失败 saveId={saveId}: {ex.Message}");
            return null;
        }
    }

    public static string GetSaveDirectory(string saveId) =>
        Path.Combine(RootPath, saveId?.Trim() ?? "");

    public static Sprite LoadPageSprite(string saveId, CompletedStoryPageFile page)
    {
        if (page == null || string.IsNullOrWhiteSpace(page.imageFile))
            return null;

        string path = Path.Combine(GetSaveDirectory(saveId), page.imageFile);
        return LoadSpriteFromFile(path);
    }

    public static Sprite LoadCoverSprite(string saveId, CompletedStoryIndexEntry entry)
    {
        if (entry == null)
            return null;

        string file = string.IsNullOrWhiteSpace(entry.coverImageFile) ? "cover.png" : entry.coverImageFile;
        return LoadSpriteFromFile(Path.Combine(GetSaveDirectory(entry.saveId), file));
    }

    static void UpsertIndex(CompletedStoryIndexEntry entry)
    {
        EnsureRoot();
        var list = new List<CompletedStoryIndexEntry>(LoadIndex());
        list.RemoveAll(e => e != null && e.saveId == entry.saveId);
        list.Insert(0, entry);
        var index = new CompletedStoryIndexFile { entries = list.ToArray() };
        File.WriteAllText(Path.Combine(RootPath, IndexFileName), JsonUtility.ToJson(index, true));
    }

    static void EnsureRoot()
    {
        if (!Directory.Exists(RootPath))
            Directory.CreateDirectory(RootPath);
    }

    static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "story";
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Trim();
    }

    static void SaveTexturePng(Texture2D texture, string path)
    {
        Texture2D readable = texture.isReadable ? texture : StoryImageUtil.DuplicateTexture(texture);
        try
        {
            File.WriteAllBytes(path, readable.EncodeToPNG());
        }
        finally
        {
            if (readable != texture && readable != null)
                UnityEngine.Object.Destroy(readable);
        }
    }

    static void SaveSpritePng(Sprite sprite, string path)
    {
        Texture2D tex = StoryImageUtil.SpriteToTexture(sprite);
        if (tex == null)
            return;

        try
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally
        {
            if (tex != sprite.texture)
                UnityEngine.Object.Destroy(tex);
        }
    }

    static Sprite LoadSpriteFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CompletedStoryStore] 加载图片失败 {path}: {ex.Message}");
            return null;
        }
    }
}
