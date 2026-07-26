namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 描述前台过渡请求，其节点图应包裹业务状态变更。
/// </summary>
public sealed class FrontedTransitionRequest
{
    /// <summary>
    /// 获取或设置应接收该过渡的前台窗口类型。
    /// </summary>
    public string WindowType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 <see cref="FrontedBehavior.TransitionTrigger"/> 使用的过渡事件类型。
    /// </summary>
    public string TransitionType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标控件的行为 GUID。
    /// </summary>
    public Guid TargetBehaviorGuid { get; set; }

    /// <summary>
    /// 获取或设置目标控件的显示名称，用于诊断和旧版回退匹配。
    /// </summary>
    public string TargetDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置稳定的、机器可读的过渡负载值。
    /// </summary>
    public Dictionary<string, object?> Payload { get; set; } = [];
}
