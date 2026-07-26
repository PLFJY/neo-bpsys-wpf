namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 行为触发条件描述。
/// </summary>
public sealed class TriggerDescriptor
{
    /// <summary>
    /// 事件类型标识。
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 事件来源。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 触发过滤器列表。
    /// </summary>
    public List<TriggerFilter> Filters { get; set; } = [];
}

