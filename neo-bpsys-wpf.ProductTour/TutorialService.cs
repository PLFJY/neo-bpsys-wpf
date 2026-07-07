using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.ProductTour.Controls;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Runs tutorial packages and flows.
/// </summary>
public interface ITutorialService
{
    /// <summary>Runs the first pending package for a page.</summary>
    /// <param name="owner">Owner element.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="triggerMode">Trigger mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> RunPendingPagePackagesAsync(
        FrameworkElement owner,
        string pageKey,
        TutorialTriggerMode triggerMode = TutorialTriggerMode.AutoOnLoaded,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a package by id.</summary>
    /// <param name="owner">Owner element.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="triggerMode">Trigger mode.</param>
    /// <param name="flowId">Optional flow id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> RunPackageAsync(
        FrameworkElement owner,
        string packageId,
        TutorialTriggerMode triggerMode,
        string? flowId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a tutorial flow.</summary>
    /// <param name="owner">Owner element.</param>
    /// <param name="flowId">Flow id.</param>
    /// <param name="force">Whether completion state should be ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run result.</returns>
    Task<TutorialRunResult> RunFlowAsync(
        FrameworkElement owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default);

    /// <summary>Resets all tutorial state.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears a flow completion record.</summary>
    /// <param name="flowId">Flow id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearFlowStateAsync(string flowId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default tutorial service.
/// </summary>
public sealed class TutorialService : ITutorialService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly ITutorialStateStore _stateStore;
    private readonly ITutorialSignalService _signalService;
    private readonly ILogger<TutorialService> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private bool _isFlowRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="TutorialService"/> class.
    /// </summary>
    /// <param name="serviceProvider">Application service provider.</param>
    /// <param name="packageRegistry">Package registry.</param>
    /// <param name="sequenceRegistry">Sequence registry.</param>
    /// <param name="flowRegistry">Flow registry.</param>
    /// <param name="stateStore">State store.</param>
    /// <param name="signalService">Signal service.</param>
    /// <param name="logger">Logger.</param>
    public TutorialService(
        IServiceProvider serviceProvider,
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry,
        ITutorialStateStore stateStore,
        ITutorialSignalService signalService,
        ILogger<TutorialService> logger)
    {
        _serviceProvider = serviceProvider;
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _flowRegistry = flowRegistry;
        _stateStore = stateStore;
        _signalService = signalService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> RunPendingPagePackagesAsync(
        FrameworkElement owner,
        string pageKey,
        TutorialTriggerMode triggerMode = TutorialTriggerMode.AutoOnLoaded,
        CancellationToken cancellationToken = default)
    {
        if (_isFlowRunning && triggerMode == TutorialTriggerMode.AutoOnLoaded)
        {
            return TutorialRunResult.Suppressed;
        }

        var state = await _stateStore.LoadAsync(cancellationToken);
        var sequence = _sequenceRegistry.GetSequence(pageKey);
        var packages = sequence
            .Select(id => _packageRegistry.GetPackage(id))
            .OfType<TutorialPackageDefinition>()
            .OrderBy(package => package.Sequence)
            .ToList();

        var pending = packages.FirstOrDefault(package => IsPackagePending(package, state)
            && (package.CanRun?.Invoke(_serviceProvider) ?? true));
        if (pending == null)
        {
            return TutorialRunResult.NotPending;
        }

        return await RunPackageAsync(owner, pending.PackageId, triggerMode, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> RunPackageAsync(
        FrameworkElement owner,
        string packageId,
        TutorialTriggerMode triggerMode,
        string? flowId = null,
        CancellationToken cancellationToken = default)
    {
        var package = _packageRegistry.GetPackage(packageId);
        if (package == null)
        {
            _logger.LogWarning("Tutorial package {PackageId} is not registered.", packageId);
            return TutorialRunResult.Failed;
        }

        if (!_runLock.Wait(0))
        {
            return triggerMode == TutorialTriggerMode.EmbeddedInFlow
                ? TutorialRunResult.Suppressed
                : TutorialRunResult.Suppressed;
        }

        try
        {
            var result = await RunStepsAsync(owner, package.Steps, flowId, package.PackageId, cancellationToken);
            if (result == TutorialRunResult.Completed && triggerMode != TutorialTriggerMode.EmbeddedInFlow)
            {
                var state = await _stateStore.LoadAsync(cancellationToken);
                state.CompletedPackages[package.PackageId] = new TutorialCompletionRecord
                {
                    Version = package.Version,
                    CompletionKind = TutorialCompletionKind.Completed
                };
                await _stateStore.SaveAsync(state, cancellationToken);
            }

            return result;
        }
        finally
        {
            _runLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<TutorialRunResult> RunFlowAsync(
        FrameworkElement owner,
        string flowId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var flow = _flowRegistry.GetFlow(flowId);
        if (flow == null)
        {
            return TutorialRunResult.Failed;
        }

        var state = await _stateStore.LoadAsync(cancellationToken);
        if (!force
            && state.CompletedFlows.TryGetValue(flowId, out var record)
            && record.Version >= flow.Version
            && record.CompletionKind == TutorialCompletionKind.Completed)
        {
            return TutorialRunResult.NotPending;
        }

        if (!_runLock.Wait(0))
        {
            return TutorialRunResult.Suppressed;
        }

        _isFlowRunning = true;
        try
        {
            foreach (var item in flow.Items)
            {
                var result = item switch
                {
                    DialogueFlowItem dialogue => await ShowDialogueAsync(owner, dialogue, cancellationToken),
                    PackageFlowItem package => await RunPackageEmbeddedAsync(owner, package.PackageId, flowId, cancellationToken),
                    ActionFlowItem action => await RunActionAsync(action, cancellationToken),
                    CustomStepFlowItem custom => await RunStepsAsync(owner, custom.Steps, flowId, null, cancellationToken),
                    _ => TutorialRunResult.Completed
                };

                if (result != TutorialRunResult.Completed && result != TutorialRunResult.NotPending)
                {
                    await MarkFlowAsync(flow, TutorialCompletionKind.Skipped, coverPackages: false, cancellationToken);
                    return result;
                }
            }

            await MarkFlowAsync(flow, TutorialCompletionKind.Completed, coverPackages: true, cancellationToken);
            return TutorialRunResult.Completed;
        }
        catch (OperationCanceledException)
        {
            return TutorialRunResult.Canceled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tutorial flow {FlowId} failed.", flowId);
            return TutorialRunResult.Failed;
        }
        finally
        {
            _isFlowRunning = false;
            _runLock.Release();
        }
    }

    /// <inheritdoc />
    public Task ResetStateAsync(CancellationToken cancellationToken = default) => _stateStore.ResetAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ClearFlowStateAsync(string flowId, CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        state.CompletedFlows.Remove(flowId);
        foreach (var package in state.CompletedPackages.Where(pair =>
                     pair.Value.CompletionKind == TutorialCompletionKind.CoveredByFlow
                     && pair.Value.SourceFlowId == flowId).Select(pair => pair.Key).ToList())
        {
            state.CompletedPackages.Remove(package);
        }

        await _stateStore.SaveAsync(state, cancellationToken);
    }

    private async Task<TutorialRunResult> RunPackageEmbeddedAsync(
        FrameworkElement owner,
        string packageId,
        string flowId,
        CancellationToken cancellationToken)
    {
        var package = _packageRegistry.GetPackage(packageId);
        if (package == null)
        {
            return TutorialRunResult.Failed;
        }

        return await RunStepsAsync(owner, package.Steps, flowId, package.PackageId, cancellationToken);
    }

    private async Task<TutorialRunResult> RunStepsAsync(
        FrameworkElement owner,
        IReadOnlyList<ProductTourStep> steps,
        string? flowId,
        string? packageId,
        CancellationToken cancellationToken)
    {
        var host = OverlayHost.GetHostPanel(owner);
        var index = 0;
        while (index < steps.Count)
        {
            var step = steps[index];
            await (step.BeforeShowAsync?.Invoke(_serviceProvider, cancellationToken) ?? Task.CompletedTask);
            FrameworkElement? target = null;
            if (!string.IsNullOrWhiteSpace(step.TargetName))
            {
                target = await TargetElementFinder.FindByNameAsync(owner, step.TargetName, step.Timeout, cancellationToken);
                if (target == null)
                {
                    if (step.AllowMissingTarget)
                    {
                        index++;
                        continue;
                    }

                    _logger.LogWarning(
                        "Tutorial target missing. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, TargetName={TargetName}",
                        flowId,
                        packageId,
                        index,
                        step.TargetName);
                    return TutorialRunResult.TargetMissing;
                }
            }

            var overlay = new ProductTourOverlay();
            var context = new ProductTourStepContext
            {
                FlowId = flowId,
                PackageId = packageId,
                StepIndex = index,
                StepCount = steps.Count,
                Owner = owner
            };
            overlay.PreviousRequested += (_, _) =>
            {
                if (index > 0)
                {
                    index -= 2;
                    overlay.CompleteExpectedAction();
                }
            };
            overlay.SkipRequested += (_, _) => overlay.CompleteExpectedAction();
            host.Children.Add(overlay);

            var stepTask = overlay.ShowStepAsync(step, target, context, cancellationToken);
            var waitTask = WaitForStepSignalAsync(step, overlay, flowId, packageId, index, cancellationToken);
            var result = await stepTask;
            await waitTask;
            await overlay.FadeOutAsync();
            host.Children.Remove(overlay);
            if (result != TutorialRunResult.Completed)
            {
                return result;
            }

            await (step.AfterCompleteAsync?.Invoke(_serviceProvider, cancellationToken) ?? Task.CompletedTask);
            index++;
        }

        return TutorialRunResult.Completed;
    }

    private async Task WaitForStepSignalAsync(
        ProductTourStep step,
        ProductTourOverlay overlay,
        string? flowId,
        string? packageId,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.WaitForSignalId))
        {
            return;
        }

        try
        {
            await _signalService.WaitAsync(step.WaitForSignalId, null, step.Timeout, cancellationToken);
            overlay.CompleteExpectedAction();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Tutorial signal timeout. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, TargetName={TargetName}, SignalId={SignalId}",
                flowId,
                packageId,
                stepIndex,
                step.TargetName,
                step.WaitForSignalId);
            overlay.CompleteExpectedAction();
        }
    }

    private async Task<TutorialRunResult> ShowDialogueAsync(
        FrameworkElement owner,
        DialogueFlowItem dialogue,
        CancellationToken cancellationToken)
    {
        var host = OverlayHost.GetHostPanel(owner);
        var overlay = new DialogueOverlay();
        host.Children.Add(overlay);
        var result = await overlay.ShowAsync(dialogue.Speaker, dialogue.Lines, cancellationToken);
        host.Children.Remove(overlay);
        return result;
    }

    private async Task<TutorialRunResult> RunActionAsync(ActionFlowItem action, CancellationToken cancellationToken)
    {
        await (action.ActionAsync?.Invoke(_serviceProvider, cancellationToken) ?? Task.CompletedTask);
        return TutorialRunResult.Completed;
    }

    private static bool IsPackagePending(TutorialPackageDefinition package, TutorialState state)
    {
        if (!state.CompletedPackages.TryGetValue(package.PackageId, out var record))
        {
            return true;
        }

        return record.Version < package.Version;
    }

    private async Task MarkFlowAsync(
        TutorialFlowDefinition flow,
        TutorialCompletionKind kind,
        bool coverPackages,
        CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        state.CompletedFlows[flow.FlowId] = new TutorialCompletionRecord
        {
            Version = flow.Version,
            CompletionKind = kind
        };

        if (coverPackages)
        {
            foreach (var packageId in flow.IncludedPackageIds)
            {
                var package = _packageRegistry.GetPackage(packageId);
                state.CompletedPackages[packageId] = new TutorialCompletionRecord
                {
                    Version = package?.Version ?? flow.Version,
                    CompletionKind = TutorialCompletionKind.CoveredByFlow,
                    SourceFlowId = flow.FlowId
                };
            }
        }

        await _stateStore.SaveAsync(state, cancellationToken);
    }
}
