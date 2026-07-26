namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 运行时注册表跟踪的活动循环行为的快照。
/// </summary>
public sealed class ActiveLoopBehaviorState
{
    /// <summary>
    /// 获取或设置行为文档标识符。
    /// </summary>
    public Guid BehaviorId { get; set; }

    /// <summary>
    /// 获取或设置所属控件的行为 GUID。
    /// </summary>
    public Guid BehaviorGuid { get; set; }

    /// <summary>
    /// 获取或设置前台窗口类型。
    /// </summary>
    public string? WindowType { get; set; }

    /// <summary>
    /// 获取或设置所属控件的显示名称。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 获取或设置活动行为模型。
    /// </summary>
    public FrontedBehavior Behavior { get; set; } = new();
}
