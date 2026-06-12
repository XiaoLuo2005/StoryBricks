#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class StoryBricksSetupBrickLibrary
{
    const string CardPrefabPath = "Assets/Prefabs/UI/StoryCard.prefab";
    const string ScenePath = "Assets/Scenes/BrickLibrary.unity";
    const string RabbitThumbPath = "Assets/Step/Rabbit/1_1x.png";
    const string HeaderTitleSpritePath = "Assets/Art/积木库.png";

    [MenuItem("StoryBricks/搭建 BrickLibrary 积木作品集")]
    public static void Setup()
    {
        StoryBricksSetupStorySummary.EnsureCardPrefabPublic();
        SetupScene();
        StoryBricksBuildSettings.EnsureSceneEnabled(ScenePath);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成",
            "已搭建 BrickLibrary（积木作品集）：\n" +
            "• 场景：Assets/Scenes/BrickLibrary.unity\n" +
            "• 已加入 Build Settings\n" +
            "• 已预置「兔子」卡片 → RabbitTutorial\n\n" +
            "新增作品：在 BrickPortfolio 的「作品列表」点 +，填写标题与教程场景名。",
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
                root.name == "StoryLibrary" ||
                root.name == "BrickPortfolio" ||
                root.name == "EventSystem" ||
                root.name.StartsWith("\u6545\u4E8B\u79EF\u6728"))
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
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(520f, 140f);
        headerRt.anchoredPosition = new Vector2(0f, -52f);
        var headerImg = header.AddComponent<Image>();
        var titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HeaderTitleSpritePath);
        headerImg.sprite = titleSprite;
        headerImg.preserveAspect = true;
        headerImg.raycastTarget = false;

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

        var lib = new GameObject("BrickPortfolio");
        var portfolio = lib.AddComponent<BrickPortfolioRoot>();
        portfolio.portfolioKind = BrickPortfolioRoot.PortfolioKind.BrickWorks;
        portfolio.cardListContent = contentRt;
        portfolio.headerTitle = "积木库";
        var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        portfolio.cardPrefab = prefabGo != null ? prefabGo.GetComponent<StoryCardView>() : null;
        portfolio.works = new[] { CreateDefaultRabbitWork() };

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

    static BrickPortfolioRoot.BrickWorkItem CreateDefaultRabbitWork()
    {
        var thumb = AssetDatabase.LoadAssetAtPath<Sprite>(RabbitThumbPath);
        return new BrickPortfolioRoot.BrickWorkItem
        {
            storyId = "rabbit",
            title = "兔子",
            tutorialSceneName = StoryFlowScenes.RabbitBuild,
            thumbnail = thumb,
        };
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
