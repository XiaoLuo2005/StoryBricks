#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按文件名前缀数字排序，将 Assets/Step/Rabbit 下 PNG 的 Sprite 写入 RabbitTutorialConfig。
/// </summary>
public static class RabbitTutorialConfigMenu
{
    const string ConfigPath = "Assets/Step/Rabbit/RabbitTutorialConfig.asset";

    [MenuItem("StoryBricks/教程/重建 Rabbit 步骤配置")]
    static void RebuildRabbitTutorialConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<TutorialStepsConfig>(ConfigPath);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<TutorialStepsConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }

        var folder = Path.Combine(Application.dataPath, "Step/Rabbit");
        if (!Directory.Exists(folder))
        {
            Debug.LogError("未找到文件夹: Assets/Step/Rabbit");
            return;
        }

        var sprites = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p =>
            {
                var name = Path.GetFileNameWithoutExtension(p);
                var head = name.Split('_')[0];
                return int.TryParse(head, out var n) ? n : 9999;
            })
            .Select(p => "Assets" + p.Substring(Application.dataPath.Length))
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null)
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogError("未加载到任何 Sprite。请确认 PNG 的 Texture Type 为 Sprite (2D and UI)。");
            return;
        }

        cfg.title = "兔子拼装教程";
        cfg.portfolioSceneName = "BrickLibrary";
        cfg.steps = sprites;
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log($"已写入 {sprites.Length} 张步骤图到 {ConfigPath}");
    }
}
#endif
