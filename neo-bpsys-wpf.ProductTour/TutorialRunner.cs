using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Public runtime entry point for tutorial execution.
/// </summary>
public interface ITutorialRunner
{
    /// <summary>
    /// Tries to run the next pending package for an owner tutorial key.
    /// </summary>
    /// <param name="owner">Tutorial owner element.</param>
    /// <param name="tutorialKey">Owner tutorial key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> TryRunNextPackageAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs pending packages until there is no next package or a package cannot complete.
    /// </summary>
    /// <param name="owner">Tutorial owner element.</param>
    /// <param name="tutorialKey">Owner tutorial key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first non-completed run result.</returns>
    Task<TutorialRunResult> RunUntilBlockedAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to run one package.
    /// </summary>
    /// <param name="owner">Tutorial owner element.</param>
    /// <param name="package">Package reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> TryRunPackageAsync(
        FrameworkElement owner,
        TutorialPackageRef package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to run one tutorial flow.
    /// </summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="flowId">Flow id.</param>
    /// <param name="force">Whether existing completion state should be ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> TryRunFlowAsync(
        Window owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default tutorial runner.
/// </summary>
public sealed class TutorialRunner : ITutorialRunner
{
    private readonly TutorialService _tutorialService;
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly ITutorialStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="TutorialRunner"/> class.
    /// </summary>
    /// <param name="tutorialService">Internal tutorial executor.</param>
    /// <param name="packageRegistry">Package registry.</param>
    /// <param name="flowRegistry">Flow registry.</param>
    /// <param name="stateStore">Tutorial state store.</param>
    internal TutorialRunner(
        TutorialService tutorialService,
        ITutorialPackageRegistry packageRegistry,
        ITutorialFlowRegistry flowRegistry,
        ITutorialStateStore stateStore)
    {
        _tutorialService = tutorialService;
        _packageRegistry = packageRegistry;
        _flowRegistry = flowRegistry;
        _stateStore = stateStore;
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> TryRunNextPackageAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default)
    {
        await WaitForUiIdleAsync(owner, cancellationToken);
        var pending = await _tutorialService.GetNextPendingPackageAsync(owner, tutorialKey, cancellationToken);
        if (pending == null)
        {
            return TutorialRunResult.NotPending;
        }

        return await _tutorialService.RunPackageAsync(
            owner,
            pending.PackageId,
            TutorialTriggerMode.Manual,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> RunUntilBlockedAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var result = await TryRunNextPackageAsync(owner, tutorialKey, cancellationToken);
            if (result == TutorialRunResult.Completed)
            {
                continue;
            }

            return result;
        }
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> TryRunPackageAsync(
        FrameworkElement owner,
        TutorialPackageRef package,
        CancellationToken cancellationToken = default)
    {
        await WaitForUiIdleAsync(owner, cancellationToken);
        var definition = _packageRegistry.GetPackage(package.Id);
        if (definition == null)
        {
            return TutorialRunResult.Failed;
        }

        if (await IsPackageCompletedAsync(definition, cancellationToken))
        {
            return TutorialRunResult.CompletedAlready;
        }

        return await _tutorialService.RunPackageAsync(
            owner,
            package.Id,
            TutorialTriggerMode.Manual,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> TryRunFlowAsync(
        Window owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        await WaitForUiIdleAsync(owner, cancellationToken);
        var flow = _flowRegistry.GetFlow(flowId);
        if (flow == null)
        {
            return TutorialRunResult.Failed;
        }

        if (!force && await IsFlowCompletedAsync(flow, cancellationToken))
        {
            return TutorialRunResult.CompletedAlready;
        }

        return await _tutorialService.RunFlowAsync(owner, flowId, force, cancellationToken);
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
