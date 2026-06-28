using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 步骤图 + 上下页按钮 + 进度条 + 文案；可配合 <see cref="SwipeStepNavigator"/> 滑动翻页。
/// </summary>
public class StepViewerUI : MonoBehaviour
{
    public Image stepImage;
    public TextMeshProUGUI stepText;
    public Button nextButton;
    public Button prevButton;
    public Slider progressBar;
    [Tooltip("换步时淡入；可空")]
    public CanvasGroup stepFadeGroup;

    public Sprite[] steps;

    [Tooltip("与 steps 对齐；可由 TutorialStepsConfig 注入")]
    public string[] stepHints;

    [Tooltip("与 steps 对齐；可由 TutorialStepsConfig 注入")]
    public TutorialStepTutorDetail[] stepTutorDetails;

    public int CurrentStepIndex => _current;
    public int StepCount => steps != null ? steps.Length : 0;

    public string GetCurrentStepHint()
    {
        if (stepHints == null || stepHints.Length == 0)
            return "";
        if (_current < 0 || _current >= stepHints.Length)
            return "";
        var h = stepHints[_current];
        return h != null ? h.Trim() : "";
    }

    public TutorialStepTutorDetail GetCurrentStepTutorDetail()
    {
        if (stepTutorDetails == null || stepTutorDetails.Length == 0)
            return null;
        if (_current < 0 || _current >= stepTutorDetails.Length)
            return null;
        return stepTutorDetails[_current];
    }

    int _current;

    void Start()
    {
        if (steps != null && steps.Length > 0)
            UpdateUI();
    }

    public void SetSteps(Sprite[] s)
    {
        steps = s;
        stepHints = null;
        stepTutorDetails = null;
        _current = 0;
        UpdateUI();
    }

    public void NextStep()
    {
        if (steps == null || steps.Length == 0)
            return;
        if (_current < steps.Length - 1)
        {
            _current++;
            UpdateUI();
        }
    }

    public void PrevStep()
    {
        if (steps == null || steps.Length == 0)
            return;
        if (_current > 0)
        {
            _current--;
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        if (steps == null || steps.Length == 0)
        {
            if (stepText != null)
                stepText.text = "无步骤图";
            return;
        }

        if (stepImage != null)
            stepImage.sprite = steps[_current];

        if (stepText != null)
            stepText.text = $"第 {_current + 1} / {steps.Length} 步";

        if (prevButton != null)
            prevButton.interactable = _current > 0;
        if (nextButton != null)
            nextButton.interactable = _current < steps.Length - 1;

        if (progressBar != null)
            progressBar.value = (float)(_current + 1) / steps.Length;

        if (stepFadeGroup != null)
        {
            StopAllCoroutines();
            StartCoroutine(FadeStepIn());
        }
    }

    IEnumerator FadeStepIn()
    {
        stepFadeGroup.alpha = 0f;
        while (stepFadeGroup.alpha < 1f)
        {
            stepFadeGroup.alpha += Time.unscaledDeltaTime * 5f;
            yield return null;
        }
        stepFadeGroup.alpha = 1f;
    }
}
