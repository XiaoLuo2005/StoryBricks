using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 教程场景顶栏：标题 + 返回积木库。可与场景中其它教程内容共存。
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class TutorialShell : MonoBehaviour
{
    [Tooltip("Build Settings 中的积木库场景名")]
    public string portfolioSceneName = "BrickLibrary";

    public string tutorialTitle = "教程";

    [Tooltip("铺满屏幕的底色（Screen Space Overlay 会盖住 3D 场景）")]
    public bool showFullScreenBackground = true;

    public Color fullScreenBackgroundColor = new Color32(252, 252, 254, 255);

    public Color barColor = new Color32(255, 255, 255, 245);
    public Color textColor = new Color32(40, 44, 52, 255);

    static Font BuiltinUIFont =>
        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void Awake()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("TutorialChromeCanvas", typeof(RectTransform));
        canvasGo.layer = LayerMask.NameToLayer("UI");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        if (showFullScreenBackground)
        {
            var bgGo = new GameObject("FullScreenBackground", typeof(RectTransform));
            bgGo.layer = LayerMask.NameToLayer("UI");
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(root, false);
            StretchFull(bgRt);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = fullScreenBackgroundColor;
            bgImg.raycastTarget = false;
        }

        var bar = new GameObject("TopBar", typeof(RectTransform));
        bar.layer = LayerMask.NameToLayer("UI");
        var barRt = bar.GetComponent<RectTransform>();
        barRt.SetParent(root, false);
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.sizeDelta = new Vector2(0f, 88f);
        barRt.anchoredPosition = Vector2.zero;

        var barImg = bar.AddComponent<Image>();
        barImg.color = barColor;

        var backGo = new GameObject("BackButton", typeof(RectTransform));
        backGo.layer = LayerMask.NameToLayer("UI");
        var backRt = backGo.GetComponent<RectTransform>();
        backRt.SetParent(barRt, false);
        backRt.anchorMin = new Vector2(0f, 0f);
        backRt.anchorMax = new Vector2(0f, 1f);
        backRt.pivot = new Vector2(0f, 0.5f);
        backRt.sizeDelta = new Vector2(200f, 0f);
        backRt.anchoredPosition = new Vector2(16f, 0f);

        var backBg = backGo.AddComponent<Image>();
        backBg.color = new Color32(235, 238, 245, 255);
        var backBtn = backGo.AddComponent<Button>();
        backBtn.targetGraphic = backBg;
        backBtn.onClick.AddListener(() =>
        {
            if (!string.IsNullOrWhiteSpace(portfolioSceneName))
                SceneManager.LoadScene(portfolioSceneName.Trim());
        });

        var backLabelGo = new GameObject("Label", typeof(RectTransform));
        backLabelGo.layer = LayerMask.NameToLayer("UI");
        var backLabelRt = backLabelGo.GetComponent<RectTransform>();
        backLabelRt.SetParent(backRt, false);
        backLabelRt.anchorMin = Vector2.zero;
        backLabelRt.anchorMax = Vector2.one;
        backLabelRt.offsetMin = Vector2.zero;
        backLabelRt.offsetMax = Vector2.zero;
        var backText = backLabelGo.AddComponent<Text>();
        backText.font = BuiltinUIFont;
        backText.fontSize = 28;
        backText.color = textColor;
        backText.alignment = TextAnchor.MiddleCenter;
        backText.text = "← 返回积木库";

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.layer = LayerMask.NameToLayer("UI");
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.SetParent(barRt, false);
        titleRt.anchorMin = new Vector2(0.35f, 0f);
        titleRt.anchorMax = new Vector2(0.65f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        var titleTx = titleGo.AddComponent<Text>();
        titleTx.font = BuiltinUIFont;
        titleTx.fontSize = 32;
        titleTx.fontStyle = FontStyle.Bold;
        titleTx.color = textColor;
        titleTx.alignment = TextAnchor.MiddleCenter;
        titleTx.text = tutorialTitle;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
