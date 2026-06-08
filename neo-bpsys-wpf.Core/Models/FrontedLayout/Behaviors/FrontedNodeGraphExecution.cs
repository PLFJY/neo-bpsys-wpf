using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public enum FrontedGraphExecutionStatus
{
    Success,
    Cancelled,
    Failed
}

public enum FrontedGraphExecutionLogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

public enum FrontedGraphActionRequestType
{
    SetProperty,
    ResetProperty,
    AnimateProperty
}

public sealed class FrontedGraphExecutionContext
{
    public Guid BehaviorGuid { get; init; }
    public string CurrentControlDisplayName { get; init; } = string.Empty;
    public string TriggerEventType { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> EventPayload { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> SelfTags { get; init; } = new Dictionary<string, object?>();
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
    public FrontedGraphActionRequestType RequestType { get; init; }
    public string Target { get; init; } = "Self";
    public string PropertyName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();
    public int? DurationMs { get; init; }
    public bool WaitForCompletion { get; init; } = true;
}

public sealed class FrontedGraphExecutionResult
{
    public FrontedGraphExecutionStatus Status { get; init; }
    public IReadOnlyList<FrontedGraphExecutionLogItem> LogItems { get; init; } = [];
    public IReadOnlyList<FrontedGraphActionRequest> ActionRequests { get; init; } = [];
    public Exception? Exception { get; init; }
}
