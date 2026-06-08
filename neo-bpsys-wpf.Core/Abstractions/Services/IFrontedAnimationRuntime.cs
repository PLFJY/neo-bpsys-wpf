using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IFrontedAnimationRuntime
{
    Task ExecuteAsync(
        IReadOnlyList<FrontedGraphActionRequest> actions,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        FrontedGraphActionRequest action,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default);

    void ResetTarget(Guid behaviorGuid, FrontedAnimationExecutionContext context);

    void ResetAll(FrontedAnimationExecutionContext context);

    /// <summary>
    /// Releases the runtime session associated with the specified root element.
    /// Cancels any in-flight animations for that session and removes it from internal tracking.
    /// </summary>
    /// <param name="root">The root <see cref="FrameworkElement"/> whose session to release.</param>
    void Release(FrameworkElement root);
}
