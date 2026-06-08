using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public enum FrontedGraphExecutionStatus
{
    /// <summary>
    /// The graph completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The graph was cancelled before completion.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The graph failed because of validation or runtime errors.
    /// </summary>
    Failed
}

public enum FrontedGraphExecutionLogLevel
{
    /// <summary>
    /// Diagnostic log item.
    /// </summary>
    Debug,

    /// <summary>
    /// Informational log item.
    /// </summary>
    Information,

    /// <summary>
    /// Warning log item.
    /// </summary>
    Warning,

    /// <summary>
    /// Error log item.
    /// </summary>
    Error
}

public enum FrontedGraphActionRequestType
{
    /// <summary>
    /// Sets a property immediately.
    /// </summary>
    SetProperty,

    /// <summary>
    /// Resets a property to its captured base value.
    /// </summary>
    ResetProperty,

    /// <summary>
    /// Animates a property over time.
    /// </summary>
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
    /// <summary>
    /// Gets the kind of action requested by the graph runtime.
    /// </summary>
    public FrontedGraphActionRequestType RequestType { get; init; }

    /// <summary>
    /// Gets the persisted target control reference.
    /// </summary>
    public string Target { get; init; } = "Self";

    /// <summary>
    /// Gets the visual layer that should receive the action.
    /// </summary>
    public FrontedAnimationTargetLayer TargetLayer { get; init; } = FrontedAnimationTargetLayer.Auto;

    /// <summary>
    /// Gets the property name to set, reset, or animate.
    /// </summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets action-specific string values such as Value, From, To, and Easing.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Gets the animation duration in milliseconds.
    /// </summary>
    public int? DurationMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the graph waits for an animation request to finish.
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
