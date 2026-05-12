using UnityEngine;

/// <summary>
/// 单步发给 AI 助教的结构化说明；在 <see cref="TutorialStepsConfig.stepTutorDetails"/> 中与 steps 对齐。
/// </summary>
[System.Serializable]
public class TutorialStepTutorDetail
{
    [Tooltip("本步要完成什么（一句话或一小段）")]
    public string stepGoal = "";

    [Tooltip("本步用到的积木：颜色、形状、编号等；可多行")]
    public string partsUsed = "";

    [Tooltip("关键顺序、对准方式、卡扣方向等")]
    public string keyActions = "";

    [Tooltip("常见错误、易混零件、安全提醒")]
    public string pitfalls = "";
}
