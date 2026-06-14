#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Assets/Art/Stories/TortoiseHare/Creation/ 下三页 PNG 绑定到 Story_TortoiseAndTheHare.creationPages。
/// </summary>
public static class StoryBricksSetupTortoiseHareCreationPages
{
    const string StoryAssetPath = "Assets/Resources/Stories/Story_TortoiseAndTheHare.asset";
    const string CreationFolder = "Assets/Art/Stories/TortoiseHare/Creation";

    static readonly (string fileName, string pageId)[] ExpectedPages =
    {
        ("P1_Start.png", "p1_start"),
        ("P2_Tree.png", "p2_tree"),
        ("P3_Finish.png", "p3_finish"),
    };

    static readonly (string fileName, int markerId, string roleName)[] ExpectedCharacters =
    {
        ("rabbit.png", 1, "兔子"),
        ("tortoise.png", 2, "乌龟"),
    };

    [MenuItem("StoryBricks/龟兔赛跑/绑定创作页背景（P1-P3）")]
    public static void BindCreationPageBackgrounds()
    {
        var def = AssetDatabase.LoadAssetAtPath<StoryDefinition>(StoryAssetPath);
        if (def == null)
        {
            EditorUtility.DisplayDialog("失败", $"未找到故事资产：{StoryAssetPath}", "好的");
            return;
        }

        if (def.creationPages == null || def.creationPages.Length != 3)
        {
            EditorUtility.DisplayDialog("失败", "Story_TortoiseAndTheHare 的 creationPages 需恰好 3 页。", "好的");
            return;
        }

        int bound = 0;
        foreach (var (fileName, pageId) in ExpectedPages)
        {
            var path = $"{CreationFolder}/{fileName}";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[StoryBricks] 缺少创作页背景：{path}");
                continue;
            }

            EnsureSpriteImport(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[StoryBricks] 无法加载 Sprite：{path}");
                continue;
            }

            for (int i = 0; i < def.creationPages.Length; i++)
            {
                if (def.creationPages[i]?.pageId != pageId)
                    continue;
                def.creationPages[i].backgroundSprite = sprite;
                bound++;
                break;
            }
        }

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            $"已绑定 {bound}/3 张创作页背景。\n\n" +
            $"目录：{CreationFolder}\n" +
            "替换 PNG 后再次执行本菜单即可刷新。",
            "好的");
    }

    [MenuItem("StoryBricks/龟兔赛跑/绑定角色参考图（兔子/乌龟）")]
    public static void BindCharacterReferences()
    {
        var def = AssetDatabase.LoadAssetAtPath<StoryDefinition>(StoryAssetPath);
        if (def == null)
        {
            EditorUtility.DisplayDialog("失败", $"未找到故事资产：{StoryAssetPath}", "好的");
            return;
        }

        if (def.characterReferences == null || def.characterReferences.Length != 2)
        {
            def.characterReferences = new[]
            {
                new StoryDefinition.CharacterReferenceEntry { markerId = 1, roleName = "兔子" },
                new StoryDefinition.CharacterReferenceEntry { markerId = 2, roleName = "乌龟" },
            };
        }

        int bound = 0;
        foreach (var (fileName, markerId, roleName) in ExpectedCharacters)
        {
            var path = $"{CreationFolder}/{fileName}";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[StoryBricks] 缺少角色参考图：{path}");
                continue;
            }

            EnsureSpriteImport(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[StoryBricks] 无法加载 Sprite：{path}");
                continue;
            }

            for (int i = 0; i < def.characterReferences.Length; i++)
            {
                if (def.characterReferences[i].markerId != markerId)
                    continue;
                def.characterReferences[i].referenceSprite = sprite;
                def.characterReferences[i].roleName = roleName;
                bound++;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(def.stylePromptPrefix))
        {
            def.stylePromptPrefix = "儿童绘本水彩插画，温暖明亮色调，横版16比9";
        }

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            $"已绑定 {bound}/2 张角色参考图（ArUco 1=兔子，2=乌龟）。\n\n" +
            $"目录：{CreationFolder}\n" +
            "替换 rabbit.png / tortoise.png 后再次执行本菜单即可刷新。",
            "好的");
    }

    static void EnsureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;
        if (importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
#endif
