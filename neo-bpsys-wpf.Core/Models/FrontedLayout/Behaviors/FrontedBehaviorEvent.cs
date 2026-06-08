namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Semantic event published through <see cref="Abstractions.Services.IFrontedEventBus" />
/// for fronted behavior runtime consumption.
/// </summary>
public sealed class FrontedBehaviorEvent
{
    /// <summary>
    /// Event type identifier matching <see cref="TriggerDescriptor.EventType" />.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Optional identifier of the fronted window that published this event.
    /// </summary>
    public string? WindowId { get; init; }

    /// <summary>
    /// Optional window type name (e.g. "BpWindow") that published this event.
    /// </summary>
    public string? WindowType { get; init; }

    /// <summary>
    /// Optional canvas name within the window.
    /// </summary>
    public string? CanvasName { get; init; }

    /// <summary>
    /// Timestamp when this event was published.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Payload key-value pairs accessible via "Event.X" paths in trigger filters.
    /// Keys are stored without the "Event." prefix.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Payload { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Optional source identifier (e.g. "SharedDataService", "WindowLifecycle").
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Whether this event is from Designer preview (not real fronted runtime).
    /// </summary>
    public bool IsPreview { get; init; }
}
