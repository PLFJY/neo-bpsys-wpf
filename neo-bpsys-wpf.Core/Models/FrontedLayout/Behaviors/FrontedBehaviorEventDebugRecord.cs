namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Captured behavior event record for the global behavior event debugger.
/// </summary>
public sealed class FrontedBehaviorEventDebugRecord
{
    /// <summary>
    /// Monotonic sequence number assigned by the debug service.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// Timestamp copied from the published behavior event.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Behavior event type.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Optional runtime window identifier.
    /// </summary>
    public string? WindowId { get; init; }

    /// <summary>
    /// Optional window type name.
    /// </summary>
    public string? WindowType { get; init; }

    /// <summary>
    /// Optional canvas name.
    /// </summary>
    public string? CanvasName { get; init; }

    /// <summary>
    /// Optional event source name.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Whether this event came from Designer preview.
    /// </summary>
    public bool IsPreview { get; init; }

    /// <summary>
    /// Formatted payload entries.
    /// </summary>
    public IReadOnlyList<FrontedBehaviorPayloadDebugEntry> Payload { get; init; } = [];
}

/// <summary>
/// Captured behavior event payload entry for debugger display and filter copy helpers.
/// </summary>
public sealed class FrontedBehaviorPayloadDebugEntry
{
    /// <summary>
    /// Payload key without the Event prefix.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Full filter path for this payload value.
    /// </summary>
    public string Path => $"Event.{Key}";

    /// <summary>
    /// Runtime value type name.
    /// </summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// Original unformatted payload value.
    /// </summary>
    public object? RawValue { get; init; }

    /// <summary>
    /// Stable display text for the payload value.
    /// </summary>
    public string DisplayValue { get; init; } = string.Empty;

    /// <summary>
    /// Stable text to paste into behavior filter values.
    /// </summary>
    public string FilterText { get; init; } = string.Empty;
}
