using System.Reflection;
using Gilzoide.LottiePlayer;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在教程 Canvas 左下角显示 Lottie 吉祥物。动画正文使用 UTF-8 的 TextAsset（扩展名建议 .txt），
/// 避免将 Lottie JSON 存为 .json 放入 Assets（会与 com.gilzoide.lottie-player 的全局 JSON 导入器冲突）。
/// </summary>
public static class TutorialMascotView
{
    const string DefaultResourcesPath = "TutorialMascot/AnimaBotLottie";

    /// <summary>
    /// 若 <paramref name="lottieJsonText"/> 为空则尝试 <see cref="Resources.Load"/> 默认路径。
    /// </summary>
    public static void TryAddToCanvas(RectTransform canvasRoot, TextAsset lottieJsonText, float bottomBarHeightPx)
    {
        var ta = lottieJsonText != null ? lottieJsonText : Resources.Load<TextAsset>(DefaultResourcesPath);
        if (ta == null || string.IsNullOrEmpty(ta.text))
            return;

        var go = new GameObject("TutorialMascot", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRoot, false);
        rt.SetAsLastSibling();

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(300f, 300f);
        rt.anchoredPosition = new Vector2(40f, bottomBarHeightPx + 28f);

        var player = go.AddComponent<ImageLottiePlayer>();
        player.raycastTarget = false;

        const int texSize = 320;
        SetPrivateField(player, "_width", texSize);
        SetPrivateField(player, "_height", texSize);
        SetPrivateField(player, "_loop", true);

        var native = new NativeLottieAnimation(ta.text, "storybricks_tutorial_mascot", "");
        if (!native.IsCreated)
        {
            Debug.LogError("TutorialMascotView: Lottie 数据无法解析，请确认导出为有效 Lottie JSON。");
            Object.Destroy(go);
            return;
        }

        player.SetAnimation(native);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        f?.SetValue(target, value);
    }
}
