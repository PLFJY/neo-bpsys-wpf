using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>教程执行的公共运行时入口点。</summary>
public interface ITutorialRunner
{
    /// <summary>运行为所有者教程键注册的每个未完成包。</summary>
    /// <param name="owner">教程所有者元素。</param>
    /// <param name="tutorialKey">所有者教程键。</param>
    /// <param name="cancellationToken">所有者生命周期的取消令牌。</param>
    /// <returns>序列运行结果。</returns>
    Task<TutorialRunResult> RunSequenceAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken = default);

    /// <summary>通过全局序列化运行一个直接请求的包。</summary>
    /// <param name="owner">教程所有者元素。</param>
    /// <param name="package">包引用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行结果。</returns>
    Task<TutorialRunResult> RunPackageAsync(
        FrameworkElement owner,
        TutorialPackageRef package,
        CancellationToken cancellationToken = default);

    /// <summary>通过全局序列化运行一个教程流程。</summary>
    /// <param name="owner">所有者窗口。</param>
    /// <param name="flowId">流程 id。</param>
    /// <param name="force">是否忽略已有的完成状态。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>运行结果。</returns>
    Task<TutorialRunResult> RunFlowAsync(
        Window owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default);
}

/// <summary>默认教程运行器。</summary>
public sealed class TutorialRunner : ITutorialRunner
{
    private readonly TutorialService _tutorialService;
    private readonly ITutorialPlaybackCoordinator _playbackCoordinator;
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly ITutorialStateStore _stateStore;
    private readonly ITutorialSessionSuppression _sessionSuppression;
    private readonly ILogger<TutorialRunner> _logger;
    private readonly ITutorialDebugService _debugService;

    internal TutorialRunner(
        TutorialService tutorialService,
        ITutorialPlaybackCoordinator playbackCoordinator,
        ITutorialPackageRegistry packageRegistry,
        ITutorialFlowRegistry flowRegistry,
        ITutorialStateStore stateStore,
        ILogger<TutorialRunner> logger,
        ITutorialDebugService? debugService = null)
        : this(tutorialService, playbackCoordinator, packageRegistry, flowRegistry, stateStore,
            new TutorialSessionSuppression(), logger, debugService)
    {
    }

    internal TutorialRunner(
        TutorialService tutorialService,
        ITutorialPlaybackCoordinator playbackCoordinator,
        ITutorialPackageRegistry packageRegistry,
        ITutorialFlowRegistry flowRegistry,
        ITutorialStateStore stateStore,
        ITutorialSessionSuppression sessionSuppression,
        ILogger<TutorialRunner> logger,
        ITutorialDebugService? debugService = null)
    {
        _tutorialService = tutorialService;
        _playbackCoordinator = playbackCoordinator;
        _packageRegistry = packageRegistry;
        _flowRegistry = flowRegistry;
        _stateStore = stateStore;
        _sessionSuppression = sessionSuppression;
        _logger = logger;
        _debugService = debugService ?? NoOpTutorialDebugService.Instance;
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

            var result = await _tutorialService.RunPackageAsync(
                owner,
                package.Id,
                TutorialTriggerMode.Manual,
                null,
                token);
            if (result == TutorialRunResult.SkippedPermanently)
            {
                await _tutorialService.MarkSequenceCompletedAsync(definition.PageKey, token);
            }
            else if (result == TutorialRunResult.Skipped)
            {
                _sessionSuppression.SuppressSequenceForCurrentSession(definition.PageKey);
            }

            return result;
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

            var packageIds = flow.Items.OfType<PackageFlowItem>().Select(item => item.PackageId).ToArray();
            _debugService.SetCurrentQueue(owner, flowId, packageIds);
            while (true)
            {
                var result = await _tutorialService.RunFlowAsync(owner, flowId, force, token);
                if (result == TutorialRunResult.Canceled && _debugService.ConsumeRestart(owner, flowId))
                {
                    continue;
                }

                return result;
            }
        }, cancellationToken);

    private async Task<TutorialRunResult> RunSequenceCoreAsync(
        FrameworkElement owner,
        string tutorialKey,
        CancellationToken cancellationToken)
    {
        if (_sessionSuppression.IsTutorialDisplaySuppressed
            || _sessionSuppression.IsSequenceSuppressedForCurrentSession(tutorialKey))
        {
            return TutorialRunResult.NotPending;
        }

        var completedAny = false;
        while (true)
        {
            await WaitForUiIdleAsync(owner, cancellationToken);
            _debugService.SetCurrentQueue(owner, tutorialKey);
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

            if (result == TutorialRunResult.Canceled && _debugService.ConsumeRestart(owner, tutorialKey))
            {
                continue;
            }

            if (result == TutorialRunResult.SkippedPermanently)
            {
                await _tutorialService.MarkSequenceCompletedAsync(tutorialKey, cancellationToken);
            }
            else if (result == TutorialRunResult.Skipped)
            {
                _sessionSuppression.SuppressSequenceForCurrentSession(tutorialKey);
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
        return _debugService.IsPackageCompleted(state, package);
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
