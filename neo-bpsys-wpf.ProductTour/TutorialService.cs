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
    private readonly ITutorialTextProvider _textProvider;
    private readonly ITutorialAvatarProvider _avatarProvider;
    private readonly ITutorialRunObserver _runObserver;
    private readonly ProductTourOptions _options;
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
    /// <param name="textProvider">Fixed UI text provider.</param>
    /// <param name="avatarProvider">Tutorial avatar provider.</param>
    /// <param name="runObserver">Tutorial run observer.</param>
    /// <param name="options">Product tour display options.</param>
    /// <param name="logger">Logger.</param>
    public TutorialService(
        IServiceProvider serviceProvider,
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry,
        ITutorialStateStore stateStore,
        ITutorialSignalService signalService,
        ITutorialTextProvider textProvider,
        ITutorialAvatarProvider avatarProvider,
        ITutorialRunObserver runObserver,
        ProductTourOptions options,
        ILogger<TutorialService> logger)
    {
        _serviceProvider = serviceProvider;
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _flowRegistry = flowRegistry;
        _stateStore = stateStore;
        _signalService = signalService;
        _textProvider = textProvider;
        _avatarProvider = avatarProvider;
        _runObserver = runObserver;
        _options = options;
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
            _runObserver.OnPackageSuppressed(pageKey);
            return TutorialRunResult.Suppressed;
        }

        var state = await _stateStore.LoadAsync(cancellationToken);
        var sequenceDefinition = _sequenceRegistry.GetSequenceDefinition(pageKey);
        var sequence = sequenceDefinition.PackageIds;
        _runObserver.OnSequenceResolved(pageKey, sequence, sequenceDefinition.AutoRunStrategy);
        var packages = sequence
            .Select(id => _packageRegistry.GetPackage(id))
            .OfType<TutorialPackageDefinition>()
            .OrderBy(package => package.Sequence)
            .ToList();

        TutorialPackageDefinition? pending = null;
        foreach (var package in packages)
        {
            if (state.CompletedPackages.TryGetValue(package.PackageId, out var record)
                && record.Version >= package.Version)
            {
                _runObserver.OnPackageSkippedByState(
                    package.PackageId,
                    record.CompletionKind,
                    record.Version,
                    package.Version);
                continue;
            }

            if (!CanRunPackage(package, owner))
            {
                _runObserver.OnPackageSkippedByCanRun(package.PackageId, pageKey);
                continue;
            }

            pending = package;
            break;
        }

        if (pending == null)
        {
            _runObserver.OnPackageNotPending(pageKey);
            return TutorialRunResult.NotPending;
        }

        _runObserver.OnPackageRunRequested(pending.PackageId, pageKey, triggerMode);
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

        if (!CanRunPackage(package, owner))
        {
            _runObserver.OnPackageSkippedByCanRun(package.PackageId, package.PageKey);
            _runObserver.OnPackageNotPending(package.PageKey);
            return TutorialRunResult.NotPending;
        }

        if (!_runLock.Wait(0))
        {
            _runObserver.OnPackageSuppressed(package.PageKey);
            return triggerMode == TutorialTriggerMode.EmbeddedInFlow
                ? TutorialRunResult.Suppressed
                : TutorialRunResult.Suppressed;
        }

        try
        {
            _runObserver.OnPackageStarted(package.PackageId, package.PageKey, triggerMode);
            var result = await RunStepsAsync(owner, package.Steps, flowId, package.PackageId, cancellationToken);
            if (result == TutorialRunResult.TargetMissing)
            {
                _runObserver.OnPackageTargetMissing(package.PackageId);
            }

            _runObserver.OnPackageCompleted(package.PackageId, result);
            if ((result == TutorialRunResult.Completed || result == TutorialRunResult.Skipped)
                && triggerMode != TutorialTriggerMode.EmbeddedInFlow)
            {
                var state = await _stateStore.LoadAsync(cancellationToken);
                state.CompletedPackages[package.PackageId] = new TutorialCompletionRecord
                {
                    Version = package.Version,
                    CompletionKind = result == TutorialRunResult.Completed
                        ? TutorialCompletionKind.Completed
                        : TutorialCompletionKind.Skipped
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
                    if (result == TutorialRunResult.Skipped)
                    {
                        await MarkFlowAsync(flow, TutorialCompletionKind.Skipped, coverPackages: false, cancellationToken);
                    }

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

        if (!CanRunPackage(package, owner))
        {
            _runObserver.OnPackageSkippedByCanRun(package.PackageId, package.PageKey);
            _runObserver.OnPackageNotPending(package.PageKey);
            return TutorialRunResult.NotPending;
        }

        _runObserver.OnPackageStarted(package.PackageId, package.PageKey, TutorialTriggerMode.EmbeddedInFlow);
        var result = await RunStepsAsync(owner, package.Steps, flowId, package.PackageId, cancellationToken);
        if (result == TutorialRunResult.TargetMissing)
        {
            _runObserver.OnPackageTargetMissing(package.PackageId);
        }

        _runObserver.OnPackageCompleted(package.PackageId, result);
        return result;
    }

    private async Task<TutorialRunResult> RunStepsAsync(
        FrameworkElement owner,
        IReadOnlyList<ProductTourStep> steps,
        string? flowId,
        string? packageId,
        CancellationToken cancellationToken)
    {
        var overlayOwner = ResolveOverlayOwner(owner);
        var host = OverlayHost.GetHostPanel(overlayOwner);
        var index = 0;
        var shownStepCount = 0;
        while (index < steps.Count)
        {
            var step = steps[index];
            await (step.BeforeShowAsync?.Invoke(_serviceProvider, cancellationToken) ?? Task.CompletedTask);
            FrameworkElement? target = null;
            if (StepRequiresTarget(step))
            {
                target = await TargetElementFinder.FindAsync(owner, step, cancellationToken);
                if (target == null)
                {
                    if (step.AllowMissingTarget)
                    {
                        _logger.LogInformation(
                            "Tutorial target missing; skipping optional step. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, TargetKey={TargetKey}",
                            flowId,
                            packageId,
                            index,
                            steps.Count,
                            step.TargetKind,
                            step.TargetName,
                            step.TargetKey);
                        index++;
                        continue;
                    }

                    _logger.LogWarning(
                        "Tutorial target missing. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, TargetKey={TargetKey}",
                        flowId,
                        packageId,
                        index,
                        steps.Count,
                        step.TargetKind,
                        step.TargetName,
                        step.TargetKey);
                    return TutorialRunResult.TargetMissing;
                }
            }

            shownStepCount++;
            _runObserver.OnStepShown(packageId ?? string.Empty, step.TargetName, step.Title);
            var overlay = new ProductTourOverlay(_textProvider, _options, _avatarProvider);
            var context = new ProductTourStepContext
            {
                FlowId = flowId,
                PackageId = packageId,
                StepIndex = index,
                StepCount = steps.Count,
                Owner = overlayOwner
            };
            host.Children.Add(overlay);

            ProductTourStepAction action;
            using (var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                try
                {
                    var stepTask = overlay.ShowStepAsync(step, target, context, stepCts.Token);
                    var waitTask = WaitForStepSignalAsync(step, overlay, flowId, packageId, index, steps.Count, stepCts.Token);
                    action = await stepTask;
                    await stepCts.CancelAsync();

                    try
                    {
                        await waitTask;
                    }
                    catch (OperationCanceledException) when (stepCts.IsCancellationRequested)
                    {
                    }
                }
                finally
                {
                    try
                    {
                        await overlay.FadeOutAsync();
                    }
                    finally
                    {
                        host.Children.Remove(overlay);
                    }
                }
            }

            if (action == ProductTourStepAction.Previous)
            {
                index = Math.Max(0, index - 1);
                continue;
            }

            if (action == ProductTourStepAction.Skip)
            {
                return TutorialRunResult.Skipped;
            }

            if (action == ProductTourStepAction.Cancel)
            {
                return TutorialRunResult.Canceled;
            }

            await (step.AfterCompleteAsync?.Invoke(_serviceProvider, cancellationToken) ?? Task.CompletedTask);
            index++;
        }

        if (steps.Count > 0 && shownStepCount == 0)
        {
            return TutorialRunResult.TargetMissing;
        }

        return TutorialRunResult.Completed;
    }

    private async Task WaitForStepSignalAsync(
        ProductTourStep step,
        ProductTourOverlay overlay,
        string? flowId,
        string? packageId,
        int stepIndex,
        int stepCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.WaitForSignalId))
        {
            return;
        }

        try
        {
            await _signalService.WaitAsync(step.WaitForSignalId, null, step.Timeout, cancellationToken);
            overlay.MarkSignalCompleted();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var message =
                $"等待操作超时。FlowId={flowId ?? "(none)"}, PackageId={packageId ?? "(none)"}, StepIndex={stepIndex}, StepCount={stepCount}, TargetKind={step.TargetKind}, TargetName={step.TargetName ?? "(none)"}, TargetKey={step.TargetKey ?? "(none)"}, SignalId={step.WaitForSignalId}";
            _logger.LogWarning(
                "Tutorial signal timeout. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, TargetKey={TargetKey}, SignalId={SignalId}",
                flowId,
                packageId,
                stepIndex,
                stepCount,
                step.TargetKind,
                step.TargetName,
                step.TargetKey,
                step.WaitForSignalId);
            overlay.MarkSignalTimedOut(message);
        }
    }

    private async Task<TutorialRunResult> ShowDialogueAsync(
        FrameworkElement owner,
        DialogueFlowItem dialogue,
        CancellationToken cancellationToken)
    {
        var overlayOwner = ResolveOverlayOwner(owner);
        var host = OverlayHost.GetHostPanel(overlayOwner);
        var overlay = new DialogueOverlay(_textProvider, _options, _avatarProvider);
        host.Children.Add(overlay);
        try
        {
            return await overlay.ShowAsync(dialogue.Speaker, dialogue.Lines, cancellationToken);
        }
        finally
        {
            host.Children.Remove(overlay);
        }
    }

    private async Task<TutorialRunResult> RunActionAsync(ActionFlowItem action, CancellationToken cancellationToken)
    {
        await (action.ActionAsync?.Invoke(_serviceProvider, cancellationToken) ?? Task.CompletedTask);
        return TutorialRunResult.Completed;
    }

    private bool CanRunPackage(TutorialPackageDefinition package, FrameworkElement owner) =>
        package.CanRunWithOwner?.Invoke(_serviceProvider, owner)
        ?? package.CanRun?.Invoke(_serviceProvider)
        ?? true;

    private static FrameworkElement ResolveOverlayOwner(FrameworkElement owner)
    {
        if (owner is Window)
        {
            return owner;
        }

        return Window.GetWindow(owner) is { } window
            ? window
            : owner;
    }

    private static bool StepRequiresTarget(ProductTourStep step) =>
        step.TargetKind switch
        {
            TutorialTargetKind.Name => !string.IsNullOrWhiteSpace(step.TargetName),
            TutorialTargetKind.NavigationItem => !string.IsNullOrWhiteSpace(step.TargetKey),
            TutorialTargetKind.DescendantType => !string.IsNullOrWhiteSpace(step.TargetKey),
            TutorialTargetKind.ElementTag => !string.IsNullOrWhiteSpace(step.TargetKey),
            _ => false
        };

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
