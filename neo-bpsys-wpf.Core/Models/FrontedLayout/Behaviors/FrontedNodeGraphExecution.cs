using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public enum FrontedGraphExecutionStatus
{
    /// <summary>
    /// 节点图成功完成。
    /// </summary>
    Success,

    /// <summary>
    /// 节点图在完成前被取消。
    /// </summary>
    Cancelled,

    /// <summary>
    /// 节点图因验证或运行时错误而失败。
    /// </summary>
    Failed
}

public enum FrontedGraphExecutionLogLevel
{
    /// <summary>
    /// 诊断日志项。
    /// </summary>
    Debug,

    /// <summary>
    /// 信息日志项。
    /// </summary>
    Information,

    /// <summary>
    /// 警告日志项。
    /// </summary>
    Warning,

    /// <summary>
    /// 错误日志项。
    /// </summary>
    Error
}

public enum FrontedGraphActionRequestType
{
    /// <summary>
    /// 立即设置属性。
    /// </summary>
    SetProperty,

    /// <summary>
    /// 将属性重置为其捕获的基准值。
    /// </summary>
    ResetProperty,

    /// <summary>
    /// 随时间对属性进行动画处理。
    /// </summary>
    AnimateProperty
}

public sealed class FrontedGraphExecutionContext
{
    /// <summary>
    /// 获取与此节点图执行关联的行为标识。
    /// </summary>
    public Guid BehaviorGuid { get; init; }

    /// <summary>
    /// 获取当前正在执行节点图的控件的显示名称。
    /// </summary>
    public string CurrentControlDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 获取提供 <see cref="EventPayload"/> 的事件类型。
    /// </summary>
    public string TriggerEventType { get; init; } = string.Empty;

    /// <summary>
    /// 获取通过 <c>Event.*</c> 解析的当前节点图事件负载。
    /// </summary>
    public IReadOnlyDictionary<string, object?> EventPayload { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 获取通过 <c>StartEvent.*</c> 解析的循环启动事件负载。
    /// </summary>
    public IReadOnlyDictionary<string, object?> StartEventPayload { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 获取通过 <c>StopEvent.*</c> 解析的循环停止事件负载。
    /// </summary>
    public IReadOnlyDictionary<string, object?> StopEventPayload { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// 获取动作节点使用的动作执行器。
    /// </summary>
    public IFrontedGraphActionExecutor? ActionExecutor { get; init; }
}

public sealed class FrontedGraphExecutionLogItem
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public FrontedGraphExecutionLogLevel Level { get; init; }
    public Guid? NodeId { get; init; }
    public required string Message { get; init; }
}

public sealed class FrontedGraphActionRequest
{
    /// <summary>
    /// 获取节点图运行时请求的动作类型。
    /// </summary>
    public FrontedGraphActionRequestType RequestType { get; init; }

    /// <summary>
    /// 获取持久化的目标控件引用。
    /// </summary>
    public string Target { get; init; } = "Self";

    /// <summary>
    /// 获取应接收该动作的视觉层。
    /// </summary>
    public FrontedAnimationTargetLayer TargetLayer { get; init; } = FrontedAnimationTargetLayer.Auto;

    /// <summary>
    /// 获取要设置、重置或动画处理的属性名称。
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// 获取动作特定的字符串值，例如 Value、From、To 和 Easing。
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// 获取动画持续时间（毫秒）。
    /// </summary>
    public int? DurationMs { get; init; }

    /// <summary>
    /// 获取指示节点图是否等待动画请求完成的值。
    /// </summary>
    public bool WaitForCompletion { get; init; } = true;
}

public sealed class FrontedGraphExecutionResult
{
    public FrontedGraphExecutionStatus Status { get; init; }
    public IReadOnlyList<FrontedGraphExecutionLogItem> LogItems { get; init; } = [];
    public IReadOnlyList<FrontedGraphActionRequest> ActionRequests { get; init; } = [];
    public Exception? Exception { get; init; }
}
