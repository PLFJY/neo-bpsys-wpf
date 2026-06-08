using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Semantic event bus for fronted behavior events.
/// Thread-safe. Publish should not crash the application on handler exceptions.
/// </summary>
public interface IFrontedEventBus
{
    /// <summary>
    /// Raised when any event is published to the bus.
    /// </summary>
    event EventHandler<FrontedBehaviorEvent>? EventPublished;

    /// <summary>
    /// Publishes a <see cref="FrontedBehaviorEvent" /> to all matching subscribers.
    /// </summary>
    void Publish(FrontedBehaviorEvent behaviorEvent);

    /// <summary>
    /// Subscribes to events of the specified <paramref name="eventType" />.
    /// When <paramref name="eventType" /> is null, subscribes to all events.
    /// </summary>
    /// <param name="eventType">Event type to filter, or null for all.</param>
    /// <param name="handler">Async handler called when a matching event is published.</param>
    /// <returns>An <see cref="IDisposable" /> that unsubscribes when disposed.</returns>
    IDisposable Subscribe(string? eventType, Func<FrontedBehaviorEvent, Task> handler);
}
