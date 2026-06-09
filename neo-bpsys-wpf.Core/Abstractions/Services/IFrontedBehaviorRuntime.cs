using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Runtime service that manages behavior execution for a fronted window.
/// Supports OneShot (event-triggered) and Loop (state machine) behaviors.
/// </summary>
public interface IFrontedBehaviorRuntime
{
    /// <summary>
    /// Attaches the runtime to a window by loading its behavior document,
    /// subscribing to the <see cref="IFrontedEventBus" />, and publishing the window layout loaded event.
    /// </summary>
    /// <param name="context">The runtime context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AttachAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches from a window, cancels running behaviors, releases event subscriptions
    /// and releases <see cref="IFrontedAnimationRuntime" /> sessions.
    /// </summary>
    /// <param name="windowId">The window identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DetachAsync(string windowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a manual trigger event to the event bus for testing or Designer preview.
    /// </summary>
    /// <param name="triggerName">The trigger name.</param>
    /// <param name="windowId">Optional window filter.</param>
    void PublishManualTrigger(string triggerName, string? windowId = null);
}
