using System.Reflection;
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
    const string CacheKey = "storybricks_tutorial_mascot";

    static readonly FieldInfo AnimationAssetField = typeof(ImageLottiePlayer).GetField(
        "_animationAsset",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo AutoPlayField = typeof(ImageLottiePlayer).GetField(
        "_autoPlay",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo WidthField = typeof(ImageLottiePlayer).GetField(
        "_width",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HeightField = typeof(ImageLottiePlayer).GetField(
        "_height",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo LoopField = typeof(ImageLottiePlayer).GetField(
        "_loop",
        BindingFlags.Instance | BindingFlags.NonPublic);

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

        // 必须挂 LottieAnimationAsset：SetAnimation(native) 会清空 _animationAsset，
        // 随后 OnEnable/OnValidate 会把 native 销毁，但 PlayRoutine 仍在跑 → Animation is null。
        var asset = ScriptableObject.CreateInstance<LottieAnimationAsset>();
        asset.hideFlags = HideFlags.HideAndDontSave;
        asset.Json = ta.text;
        asset.CacheKey = CacheKey;
        asset.ResourcePath = "";
        if (!asset.UpdateMetadata())
        {
            Debug.LogError("TutorialMascotView: Lottie 数据无法解析，请确认导出为有效 Lottie JSON。");
            Object.Destroy(asset);
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

        AnimationAssetField?.SetValue(player, asset);
        AutoPlayField?.SetValue(player, AutoPlayEvent.OnEnable);
        WidthField?.SetValue(player, 320);
        HeightField?.SetValue(player, 320);
        LoopField?.SetValue(player, true);

        go.AddComponent<TutorialMascotAssetOwner>().Bind(asset);
        go.SetActive(true);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

/// <summary>销毁吉祥物时一并释放运行时创建的 LottieAnimationAsset。</summary>
[DisallowMultipleComponent]
sealed class TutorialMascotAssetOwner : MonoBehaviour
{
    LottieAnimationAsset _asset;

    public void Bind(LottieAnimationAsset asset)
    {
        _asset = asset;
    }

    void OnDestroy()
    {
        if (_asset != null)
        {
            Destroy(_asset);
            _asset = null;
        }
    }
}
