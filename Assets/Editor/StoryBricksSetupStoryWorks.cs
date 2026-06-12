#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class StoryBricksSetupStoryWorks
{
    const string CardPrefabPath = "Assets/Prefabs/UI/StoryCard.prefab";
    const string ScenePath = "Assets/Scenes/StoryWorks.unity";

    [MenuItem("StoryBricks/搭建 StoryWorks 故事作品集")]
    public static void Setup()
    {
        StoryBricksSetupStorySummary.EnsureCardPrefabPublic();
        SetupScene();
        StoryBricksBuildSettings.EnsureSceneEnabled(ScenePath);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            "已搭建 StoryWorks（故事作品集）：\n" +
            "• 场景：Assets/Scenes/StoryWorks.unity\n" +
            "• 已加入 Build Settings\n\n" +
            "流程：StorySummary → StoryPrologue → StoryWorks（仅显示当前故事的作品）→ 教程场景\n\n" +
            "请在各 StoryDefinition 的 Works 里配置该故事包含的积木作品。",
            "好的");
    }

    static void SetupScene()
    {
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        foreach (var root in scene.GetRootGameObjects().ToArray())
        {
            if (root.GetComponent<Canvas>() != null ||
                root.name == "StoryWorksPortfolio" ||
                root.name == "EventSystem")
                Object.DestroyImmediate(root);
        }

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
        headerTmp.text = "故事作品集";
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

        var lib = new GameObject("StoryWorksPortfolio");
        var portfolio = lib.AddComponent<BrickPortfolioRoot>();
        portfolio.portfolioKind = BrickPortfolioRoot.PortfolioKind.StoryWorks;
        portfolio.headerTitleTextTmp = headerTmp;
        portfolio.cardListContent = contentRt;
        portfolio.headerTitle = "故事作品集";
        var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        portfolio.cardPrefab = prefabGo != null ? prefabGo.GetComponent<StoryCardView>() : null;

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        if (!File.Exists(ScenePath))
            EditorSceneManager.SaveScene(scene, ScenePath);
        else
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
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
