namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 描述活动循环行为被请求停止的原因。
/// </summary>
public enum FrontedBehaviorStopReason
{
    /// <summary>
    /// 用户手动清除了活动循环动画。
    /// </summary>
    ManualClear,

    /// <summary>
    /// 对局引导被取消。
    /// </summary>
    GuidanceCancelled,

    /// <summary>
    /// 对局引导被停止或完成。
    /// </summary>
    GuidanceStopped,

    /// <summary>
    /// 前台窗口被隐藏或关闭。
    /// </summary>
    WindowHidden,

    /// <summary>
    /// 活动布局包被切换。
    /// </summary>
    PackageSwitched,

    /// <summary>
    /// 前台布局被重新加载。
    /// </summary>
    LayoutReloaded
}

