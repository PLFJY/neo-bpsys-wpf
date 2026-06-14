namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台填充行为策略。
/// </summary>
public enum FrontedFillBehavior
{
    /// <summary>
    /// 保持最终值。
    /// </summary>
    HoldEnd,

    /// <summary>
    /// 重置为初始值。
    /// </summary>
    Reset,

    /// <summary>
    /// 停止当前动画。
    /// </summary>
    StopCurrent
}

