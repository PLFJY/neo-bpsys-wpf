using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Attributes;

/// <summary>
/// 前台行为事件特性，用于标记事件类型及其元数据。
/// </summary>
/// <param name="eventType">事件类型标识</param>
[AttributeUsage(AttributeTargets.Event)]
public sealed class FrontedBehaviorEventAttribute(string eventType) : Attribute
{
    /// <summary>
    /// 事件类型标识
    /// </summary>
    public string EventType { get; } = eventType;

    /// <summary>
    /// 显示名称的本地化键
    /// </summary>
    public string DisplayNameKey { get; init; } = string.Empty;

    /// <summary>
    /// 描述文本的本地化键
    /// </summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>
    /// 分类的本地化键
    /// </summary>
    public string CategoryKey { get; init; } = string.Empty;

    /// <summary>
    /// 分类名称
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 排序序号
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// 前台行为事件负载特性，用于标记事件参数的元数据。
/// </summary>
/// <param name="path">负载数据路径</param>
[AttributeUsage(AttributeTargets.Event, AllowMultiple = true)]
public sealed class FrontedBehaviorEventPayloadAttribute(string path) : Attribute
{
    /// <summary>
    /// 负载数据路径
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// 显示名称的本地化键
    /// </summary>
    public string DisplayNameKey { get; init; } = string.Empty;

    /// <summary>
    /// 描述文本的本地化键
    /// </summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>
    /// 值类型
    /// </summary>
    public Type? ValueType { get; init; }

    /// <summary>
    /// 类型名称
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>
    /// 负载来源
    /// </summary>
    public FrontedBehaviorPayloadSource Source { get; init; }

    /// <summary>
    /// 来源路径
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// 是否为通用过滤目标
    /// </summary>
    public bool IsCommonFilterTarget { get; init; } = true;
}
