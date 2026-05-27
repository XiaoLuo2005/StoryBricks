#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class StoryBricksSetupStorySummary
{
    const string CardPrefabPath = "Assets/Prefabs/UI/StoryCard.prefab";
    const string ScenePath = "Assets/Scenes/StorySummary.unity";

    [MenuItem("StoryBricks/搭建 StorySummary 多故事库")]
    public static void Setup()
    {
        EnsureCardPrefabPublic();
        SetupScene();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            "已搭建 StorySummary：\n• StoryCard.prefab（含 StoryCardView，支持 TMP）\n• ScrollView + StoryLibrary\n\n请在 StoryCatalog 的 Stories 里拖入故事资产。",
            "好的");
    }

    public static void EnsureCardPrefabPublic() => EnsureCardPrefab();

    static void EnsureCardPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) != null)
            return;

        var root = new GameObject("StoryCard", typeof(RectTransform), typeof(Image), typeof(StoryCardView));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(320, 380);
        root.GetComponent<Image>().color = Color.white;

        var cover = Child(root.transform, "Cover");
        SetAnchors(cover, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.92f));
        var coverImg = cover.AddComponent<Image>();
        coverImg.preserveAspect = true;
        coverImg.color = new Color32(230, 233, 239, 255);

        var title = Child(root.transform, "Title");
        SetAnchors(title, new Vector2(0f, 0.2f), new Vector2(1f, 0.3f));
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "故事名";
        titleTmp.fontSize = 22;
        titleTmp.alignment = TextAlignmentOptions.Center;

        var btn = Child(root.transform, "ChooseButton");
        SetAnchors(btn, new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.18f));
        var btnImg = btn.AddComponent<Image>();
        btnImg.color = new Color32(66, 133, 244, 255);
        var button = btn.AddComponent<Button>();
        button.targetGraphic = btnImg;
        var label = Child(btn.transform, "Label");
        Stretch(label.GetComponent<RectTransform>());
        var labelTmp = label.AddComponent<TextMeshProUGUI>();
        labelTmp.text = "选择";
        labelTmp.fontSize = 24;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = Color.white;
        labelTmp.alignment = TextAlignmentOptions.Center;

        var view = root.GetComponent<StoryCardView>();
        view.coverImage = coverImg;
        view.titleTextTmp = titleTmp;
        view.chooseButton = button;

        PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        Object.DestroyImmediate(root);
    }

    static void SetupScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var c in Object.FindObjectsOfType<Canvas>())
            if (c.gameObject.name != "Canvas" || true)
                Object.DestroyImmediate(c.gameObject);
        var old = GameObject.Find("StoryLibrary");
        if (old != null)
            Object.DestroyImmediate(old);
        var oldCard = GameObject.Find("StoryCard");
        if (oldCard != null)
            Object.DestroyImmediate(oldCard);

        var canvas = new GameObject("Canvas");
        var cnv = canvas.AddComponent<Canvas>();
        cnv.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvas.AddComponent<GraphicRaycaster>();
        Stretch(canvas.GetComponent<RectTransform>());

        var header = Child(canvas.transform, "HeaderTitle");
        var headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0, 1);
        headerRt.anchorMax = new Vector2(1, 1);
        headerRt.offsetMin = new Vector2(0, -100);
        headerRt.offsetMax = Vector2.zero;
        var headerTmp = header.AddComponent<TextMeshProUGUI>();
        headerTmp.text = "故事库";
        headerTmp.fontSize = 40;
        headerTmp.alignment = TextAlignmentOptions.Center;

        var scroll = Child(canvas.transform, "ScrollView");
        Stretch(scroll.GetComponent<RectTransform>());
        scroll.GetComponent<RectTransform>().offsetMax = new Vector2(0, -110);
        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewport = Child(scroll.transform, "Viewport");
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var content = Child(viewport.transform, "Content");
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 800);
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(320, 380);
        grid.spacing = new Vector2(28, 28);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRt;

        var lib = new GameObject("StoryLibrary");
        var portfolio = lib.AddComponent<BrickPortfolioRoot>();
        var catalog = lib.AddComponent<StoryCatalog>();
        portfolio.headerTitleTextTmp = headerTmp;
        portfolio.cardListContent = contentRt;
        portfolio.headerTitle = "故事库";
        var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        portfolio.cardPrefab = prefabGo != null ? prefabGo.GetComponent<StoryCardView>() : null;

        var tortoise = AssetDatabase.LoadAssetAtPath<StoryDefinition>("Assets/Resources/Stories/Story_TortoiseHare.asset");
        if (tortoise != null)
            catalog.stories = new[] { tortoise };

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
