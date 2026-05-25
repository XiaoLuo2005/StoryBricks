using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class StoryProloguePictureBook : MonoBehaviour
{
    public string fallbackLibrarySceneName = StoryFlowScenes.StoryLibrary;
    public Image pageImage;
    public TextMeshProUGUI pageCaptionTextTmp;
    public TextMeshProUGUI pageIndicatorTextTmp;
    public Button prevPageButton;
    public Button nextPageButton;
    public Button startBuildButton;

    Sprite[] _pages;
    int _index;

    void Start()
    {
        if (!StorySelectionContext.HasSelection)
        {
            SceneManager.LoadScene(fallbackLibrarySceneName.Trim());
            return;
        }

        _pages = StorySelectionContext.ProloguePages;
        if (_pages == null || _pages.Length == 0)
        {
            if (StorySelectionContext.Cover != null)
                _pages = new[] { StorySelectionContext.Cover };
            else
                return;
        }

        _index = 0;
        WireButtons();
        ShowPage(_index);
    }

    void WireButtons()
    {
        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveAllListeners();
            prevPageButton.onClick.AddListener(PrevPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.AddListener(NextPage);
        }
        if (startBuildButton != null)
        {
            startBuildButton.onClick.RemoveAllListeners();
            startBuildButton.onClick.AddListener(() =>
                SceneManager.LoadScene(StorySelectionContext.BuildSceneName.Trim()));
        }
    }

    void ShowPage(int index)
    {
        if (pageImage != null && index >= 0 && index < _pages.Length)
        {
            pageImage.sprite = _pages[index];
            pageImage.preserveAspect = true;
        }

        string indicator = $"{index + 1} / {_pages.Length}";
        if (pageIndicatorTextTmp != null)
            pageIndicatorTextTmp.text = indicator;

        if (prevPageButton != null)
            prevPageButton.interactable = index > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = index < _pages.Length - 1;
    }

    public void PrevPage()
    {
        if (_index <= 0)
            return;
        _index--;
        ShowPage(_index);
    }

    public void NextPage()
    {
        if (_index >= _pages.Length - 1)
            return;
        _index++;
        ShowPage(_index);
    }
}
