using Gilzoide.LottiePlayer;
using UnityEngine;

/// <summary>
/// 在教程 Canvas 左下角显示 Lottie 吉祥物。动画正文使用 UTF-8 的 TextAsset（扩展名建议 .txt），
/// 避免将 Lottie JSON 存为 .json 放入 Assets（会与 com.gilzoide.lottie-player 的全局 JSON 导入器冲突）。
/// </summary>
public static class TutorialMascotView
{
    const string DefaultResourcesPath = "TutorialMascot/AnimaBotLottie";
    const string MascotObjectName = "TutorialMascot";

    /// <summary>
    /// 在教程页吉祥物锚点显示 Lottie。若 anchor 为空则跳过。
    /// </summary>
    public static void TryAddToCanvas(RectTransform mascotAnchor, TextAsset lottieJsonText)
    {
        if (mascotAnchor == null)
        {
            Debug.LogWarning("[TutorialMascotView] 未指定 mascotRoot，跳过吉祥物。");
            return;
        }

        if (mascotAnchor.Find(MascotObjectName) != null)
            return;

        var ta = lottieJsonText != null ? lottieJsonText : Resources.Load<TextAsset>(DefaultResourcesPath);
        if (ta == null || string.IsNullOrWhiteSpace(ta.text))
        {
            Debug.LogWarning("[TutorialMascotView] 未找到 Lottie 文本资源，跳过吉祥物。");
            return;
        }

        var go = new GameObject(MascotObjectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = LayerMask.NameToLayer("UI");
        go.SetActive(false);

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(mascotAnchor, false);
        StretchFull(rt);

        var player = go.AddComponent<ImageLottiePlayer>();
        player.raycastTarget = false;

        // 必须先关掉自动播放；OnEnable 会清空 animation，OnStart 会在 SetAnimation 之前 Play。
        SetSerializedField(player, "_autoPlay", AutoPlayEvent.No);
        SetSerializedField(player, "_width", 320);
        SetSerializedField(player, "_height", 320);
        SetSerializedField(player, "_loop", true);

        var native = new NativeLottieAnimation(ta.text, "storybricks_tutorial_mascot", "");
        if (!native.IsCreated)
        {
            Debug.LogError("TutorialMascotView: Lottie 数据无法解析，请确认导出为有效 Lottie JSON。");
            Object.Destroy(go);
            return;
        }

        go.SetActive(true);
        player.SetAnimation(native);
        player.Play();
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetSerializedField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
