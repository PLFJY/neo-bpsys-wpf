namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 通过 <see cref="Abstractions.Services.IFrontedEventBus" /> 发布、供前台行为运行时消费的语义事件。
/// </summary>
public sealed class FrontedBehaviorEvent
{
    /// <summary>
    /// 事件类型标识符，与 <see cref="TriggerDescriptor.EventType" /> 匹配。
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 发布此事件的前台窗口的可选标识符。
    /// </summary>
    public string? WindowId { get; init; }

    /// <summary>
    /// 发布此事件的可选窗口类型名（例如 "BpWindow"）。
    /// </summary>
    public string? WindowType { get; init; }

    /// <summary>
    /// 窗口内的可选画布名称。
    /// </summary>
    public string? CanvasName { get; init; }

    /// <summary>
    /// 发布此事件的时间戳。
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 可通过触发条件过滤器中的 "Event.X" 路径访问的负载键值对。
    /// 键存储时不带 "Event." 前缀。
    /// </summary>
    public IReadOnlyDictionary<string, object?> Payload { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 可选的来源标识符（例如 "SharedDataService"、"WindowLifecycle"）。
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 此事件是否来自设计器预览（而非真实前台运行时）。
    /// </summary>
    public bool IsPreview { get; init; }
}
