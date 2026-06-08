using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Runtime service that manages behavior execution for a fronted Canvas.
/// Supports OneShot (event-triggered) and Loop (state machine) behaviors.
/// </summary>
public interface IFrontedBehaviorRuntime
{
    /// <summary>
    /// Attaches the runtime to a Canvas by loading its behavior document,
    /// subscribing to the <see cref="IFrontedEventBus" />, and publishing CanvasLoaded.
    /// </summary>
    Task AttachAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches from a Canvas, cancels running behaviors, releases event subscriptions
    /// and releases <see cref="IFrontedAnimationRuntime" /> sessions.
    /// </summary>
    Task DetachAsync(string windowId, string canvasName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a manual trigger event to the event bus for testing or Designer preview.
    /// </summary>
    void PublishManualTrigger(string triggerName, string? windowId = null, string? canvasName = null);
}
