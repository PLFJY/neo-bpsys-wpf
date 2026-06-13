using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Runs fronted transition behavior graphs around business state changes.
/// </summary>
public interface IFrontedTransitionOrchestrator
{
    /// <summary>
    /// Runs matching transition exit graphs, commits the business state change, then runs matching enter graphs.
    /// </summary>
    /// <param name="request">Transition request for one target control.</param>
    /// <param name="commitAsync">Business state update to run between exit and enter graphs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes after the transition sequence finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="commitAsync"/> is null.</exception>
    Task RunTransitionAsync(
        FrontedTransitionRequest request,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs matching exit graphs for all targets, commits once, then runs matching enter graphs for all targets.
    /// </summary>
    /// <param name="requests">Transition requests for all target controls.</param>
    /// <param name="commitAsync">Business state update to run between exit and enter graphs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes after the multi-target transition sequence finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requests"/> or <paramref name="commitAsync"/> is null.</exception>
    Task RunMultiTargetTransitionAsync(
        IReadOnlyList<FrontedTransitionRequest> requests,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default);
}
