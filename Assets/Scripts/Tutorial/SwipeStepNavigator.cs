using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在步骤显示区：仅在按下/抬起发生在本 UI 上时识别水平滑动并翻页（鼠标与触摸）。
/// </summary>
public class SwipeStepNavigator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public StepViewerUI viewer;
    public float minSwipeDistance = 120f;

    Vector2 _start;
    bool _down;

    public void OnPointerDown(PointerEventData eventData)
    {
        _down = true;
        _start = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_down || viewer == null)
            return;
        _down = false;
        float dx = eventData.position.x - _start.x;
        if (dx > minSwipeDistance)
            viewer.PrevStep();
        else if (dx < -minSwipeDistance)
            viewer.NextStep();
    }
}
