namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 为全局行为事件调试器捕获的行为事件记录。
/// </summary>
public sealed class FrontedBehaviorEventDebugRecord
{
    /// <summary>
    /// 由调试服务分配的单调递增序列号。
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// 从已发布的行为事件复制的时间戳。
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// 行为事件类型。
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 可选的运行时窗口标识。
    /// </summary>
    public string? WindowId { get; init; }

    /// <summary>
    /// 可选的窗口类型名称。
    /// </summary>
    public string? WindowType { get; init; }

    /// <summary>
    /// 可选的画布名称。
    /// </summary>
    public string? CanvasName { get; init; }

    /// <summary>
    /// 可选的事件来源名称。
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 指示此事件是否来自设计器预览。
    /// </summary>
    public bool IsPreview { get; init; }

    /// <summary>
    /// 已格式化的负载条目。
    /// </summary>
    public IReadOnlyList<FrontedBehaviorPayloadDebugEntry> Payload { get; init; } = [];
}

/// <summary>
/// 为调试器显示和过滤器复制助手捕获的行为事件负载条目。
/// </summary>
public sealed class FrontedBehaviorPayloadDebugEntry
{
    /// <summary>
    /// 不带 Event 前缀的负载键。
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 此负载值的完整过滤器路径。
    /// </summary>
    public string Path => $"Event.{Key}";

    /// <summary>
    /// 运行时值类型名称。
    /// </summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// 原始未格式化的负载值。
    /// </summary>
    public object? RawValue { get; init; }

    /// <summary>
    /// 负载值的稳定显示文本。
    /// </summary>
    public string DisplayValue { get; init; } = string.Empty;

    /// <summary>
    /// 用于粘贴到行为过滤器值中的稳定文本。
    /// </summary>
    public string FilterText { get; init; } = string.Empty;
}
