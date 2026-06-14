namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为事件描述，提供事件类型、显示信息和负载字段定义。
/// </summary>
public sealed class FrontedBehaviorEventDescriptor
{
    /// <summary>
    /// 事件类型标识。
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称的本地化键。
    /// </summary>
    public string DisplayNameKey { get; set; } = string.Empty;

    /// <summary>
    /// 描述的本地化键。
    /// </summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// 事件分类。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 分类显示名称的本地化键。
    /// </summary>
    public string CategoryDisplayNameKey { get; set; } = string.Empty;

    /// <summary>
    /// 排序序号。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 事件负载字段列表。
    /// </summary>
    public List<FrontedBehaviorEventPayloadField> PayloadFields { get; set; } = [];
}
