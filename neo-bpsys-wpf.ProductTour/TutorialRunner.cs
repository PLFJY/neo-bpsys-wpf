using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>Public runtime entry point for tutorial execution.</summary>
public interface ITutorialRunner
{
    /// <summary>Runs every unfinished package registered for an owner tutorial key.</summary>
    /// <param name="owner">Tutorial owner element.</param>
    /// <param name="tutorialKey">Owner tutorial key.</param>
    /// <param name="cancellationToken">Owner lifetime cancellation token.</param>
    /// <returns>The sequence result.</returns>
    Task<TutorialRunResult> RunSequenceAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default);

    /// <summary>Runs one directly requested package through global serialization.</summary>
    /// <param name="owner">Tutorial owner element.</param>
    /// <param name="package">Package reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> RunPackageAsync(
        FrameworkElement owner,
        TutorialPackageRef package,
        CancellationToken cancellationToken = default);

    /// <summary>Runs one tutorial flow through global serialization.</summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="flowId">Flow id.</param>
    /// <param name="force">Whether existing completion state should be ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> RunFlowAsync(
        Window owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default);
}

/// <summary>Default tutorial runner.</summary>
public sealed class TutorialRunner : ITutorialRunner
{
    private readonly TutorialService _tutorialService;
    private readonly ITutorialPlaybackCoordinator _playbackCoordinator;
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly ITutorialStateStore _stateStore;
    private readonly ILogger<TutorialRunner> _logger;

    internal TutorialRunner(
        TutorialService tutorialService,
        ITutorialPlaybackCoordinator playbackCoordinator,
        ITutorialPackageRegistry packageRegistry,
        ITutorialFlowRegistry flowRegistry,
        ITutorialStateStore stateStore,
        ILogger<TutorialRunner> logger)
    {
        _tutorialService = tutorialService;
        _playbackCoordinator = playbackCoordinator;
        _packageRegistry = packageRegistry;
        _flowRegistry = flowRegistry;
        _stateStore = stateStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> RunSequenceAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default)
    {
        return await _playbackCoordinator.RunSequenceAsync(
            owner,
            tutorialKey,
            token => RunSequenceCoreAsync(owner, tutorialKey, token),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<TutorialRunResult> RunPackageAsync(
        FrameworkElement owner,
        TutorialPackageRef package,
        CancellationToken cancellationToken = default) =>
        _playbackCoordinator.RunAsync(owner, package.Id, async token =>
        {
            await WaitForUiIdleAsync(owner, token);
            var definition = _packageRegistry.GetPackage(package.Id);
            if (definition == null)
            {
                return TutorialRunResult.Failed;
            }

            if (await IsPackageCompletedAsync(definition, token))
            {
                return TutorialRunResult.CompletedAlready;
            }

            return await _tutorialService.RunPackageAsync(
                owner,
                package.Id,
                TutorialTriggerMode.Manual,
                null,
                token);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<TutorialRunResult> RunFlowAsync(
        Window owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default) =>
        _playbackCoordinator.RunAsync(owner, flowId, async token =>
        {
            await WaitForUiIdleAsync(owner, token);
            var flow = _flowRegistry.GetFlow(flowId);
            if (flow == null)
            {
                return TutorialRunResult.Failed;
            }

            if (!force && await IsFlowCompletedAsync(flow, token))
            {
                return TutorialRunResult.CompletedAlready;
            }

            return await _tutorialService.RunFlowAsync(owner, flowId, force, token);
        }, cancellationToken);

    private async Task<TutorialRunResult> RunSequenceCoreAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken)
    {
        var completedAny = false;
        while (true)
        {
            await WaitForUiIdleAsync(owner, cancellationToken);
            var pending = await _tutorialService.GetNextPendingPackageAsync(owner, tutorialKey, cancellationToken);
            if (pending == null)
            {
                _logger.LogInformation(
                    "Tutorial sequence completed. TutorialKey={TutorialKey}, OwnerType={OwnerType}",
                    tutorialKey,
                    owner.GetType().FullName);
                return completedAny ? TutorialRunResult.Completed : TutorialRunResult.NotPending;
            }

            _logger.LogInformation(
                "Tutorial package started. TutorialKey={TutorialKey}, PackageId={PackageId}",
                tutorialKey,
                pending.PackageId);
            var result = await _tutorialService.RunPackageAsync(
                owner,
                pending.PackageId,
                TutorialTriggerMode.AutoOnLoaded,
                null,
                cancellationToken);
            _logger.LogInformation(
                "Tutorial package result. TutorialKey={TutorialKey}, PackageId={PackageId}, Result={Result}",
                tutorialKey,
                pending.PackageId,
                result);

            if (result is TutorialRunResult.Completed or TutorialRunResult.CompletedAlready)
            {
                completedAny = true;
                continue;
            }

            _logger.LogInformation(
                "Tutorial sequence blocked. TutorialKey={TutorialKey}, PackageId={PackageId}, Reason={Reason}",
                tutorialKey,
                pending.PackageId,
                result);
            return result;
        }
    }

    private async Task<bool> IsPackageCompletedAsync(
        TutorialPackageDefinition package,
        CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        return state.CompletedPackages.TryGetValue(package.PackageId, out var record)
            && record.Version >= package.Version;
    }

    private async Task<bool> IsFlowCompletedAsync(
        TutorialFlowDefinition flow,
        CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        return state.CompletedFlows.TryGetValue(flow.FlowId, out var record)
            && record.Version >= flow.Version
            && record.CompletionKind == TutorialCompletionKind.Completed;
    }

    private static async Task WaitForUiIdleAsync(
        FrameworkElement owner,
        CancellationToken cancellationToken)
    {
        await owner.Dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }
}
