using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.ProductTour.Controls;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Manages persisted tutorial state.
/// </summary>
public interface ITutorialStateManager
{
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
internal sealed class TutorialService : ITutorialStateManager, ITutorialStepCancellation
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
    private ProductTourOverlay? _currentOverlay;

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
    internal TutorialService(
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
    public async Task<TutorialPackageDefinition?> GetNextPendingPackageAsync(
        FrameworkElement owner,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        var sequenceDefinition = _sequenceRegistry.GetSequenceDefinition(pageKey);
        var sequence = sequenceDefinition.PackageIds;
        _runObserver.OnSequenceResolved(pageKey, sequence);
        var packages = sequence
            .Select(id => _packageRegistry.GetPackage(id))
            .OfType<TutorialPackageDefinition>()
            .OrderBy(package => package.Sequence)
            .ToList();

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

            return package;
        }

        return null;
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
            _runObserver.OnPackageNotReady(package.PackageId, package.PageKey);
            return TutorialRunResult.NotReady;
        }

        _runObserver.OnPackageStarted(package.PackageId, package.PageKey, triggerMode);
        var result = await RunPackageItemsAsync(owner, package.Items, flowId, package.PackageId, cancellationToken);
        if (result == TutorialRunResult.TargetMissing)
        {
            _runObserver.OnPackageTargetMissing(package.PackageId);
        }

        _runObserver.OnPackageCompleted(package.PackageId, result);
        var completionStateWritten = false;
        if ((result is TutorialRunResult.Completed
                or TutorialRunResult.Skipped
                or TutorialRunResult.ChildWindowHandoff)
            && triggerMode != TutorialTriggerMode.EmbeddedInFlow)
        {
            var state = await _stateStore.LoadAsync(cancellationToken);
            state.CompletedPackages[package.PackageId] = new TutorialCompletionRecord
            {
                Version = package.Version,
                CompletionKind = TutorialCompletionKind.Completed
            };
            await _stateStore.SaveAsync(state, cancellationToken);
            completionStateWritten = true;
        }

        _logger.LogInformation(
            "Tutorial package finalized. PackageId={PackageId}, PageKey={PageKey}, OwnerType={OwnerType}, FinalResult={FinalResult}, CompletionStateWritten={CompletionStateWritten}, CancellationRequested={CancellationRequested}",
            package.PackageId,
            package.PageKey,
            owner.GetType().FullName,
            result,
            completionStateWritten,
            cancellationToken.IsCancellationRequested);

        return result;
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
                        await MarkFlowAsync(
                            flow,
                            TutorialCompletionKind.Completed,
                            coverPackages: true,
                            cancellationToken,
                            includedPackageCompletionKind: TutorialCompletionKind.Completed);
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
    }

    /// <inheritdoc />
    public Task ResetStateAsync(CancellationToken cancellationToken = default) => _stateStore.ResetAsync(cancellationToken);

    /// <inheritdoc />
    public void YieldCurrentStepForChildWindow() =>
        _currentOverlay?.ForceComplete(ProductTourStepAction.ChildWindowHandoff);

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
            _runObserver.OnPackageNotReady(package.PackageId, package.PageKey);
            return TutorialRunResult.NotReady;
        }

        _runObserver.OnPackageStarted(package.PackageId, package.PageKey, TutorialTriggerMode.EmbeddedInFlow);
        var result = await RunPackageItemsAsync(owner, package.Items, flowId, package.PackageId, cancellationToken);
        if (result == TutorialRunResult.TargetMissing)
        {
            _runObserver.OnPackageTargetMissing(package.PackageId);
        }

        _runObserver.OnPackageCompleted(package.PackageId, result);
        return result;
    }

    private async Task<TutorialRunResult> RunPackageItemsAsync(
        FrameworkElement owner,
        IReadOnlyList<TutorialPackageItem> items,
        string? flowId,
        string packageId,
        CancellationToken cancellationToken)
    {
        var index = 0;
        while (index < items.Count)
        {
            _logger.LogInformation(
                "Tutorial package item transitioning. PackageId={PackageId}, PackageItemIndex={PackageItemIndex}, ItemCount={ItemCount}, ItemType={ItemType}, OwnerType={OwnerType}, CancellationRequested={CancellationRequested}",
                packageId,
                index,
                items.Count,
                items[index].GetType().Name,
                owner.GetType().FullName,
                cancellationToken.IsCancellationRequested);

            if (items[index] is TutorialPackageDialogueItem dialogueItem)
            {
                var dialogueResult = await ShowDialogueAsync(owner, dialogueItem.Dialogue, cancellationToken);
                _logger.LogInformation(
                    "Tutorial dialogue item resolved. PackageId={PackageId}, PackageItemIndex={PackageItemIndex}, Result={Result}",
                    packageId,
                    index,
                    dialogueResult);
                if (dialogueResult != TutorialRunResult.Completed)
                {
                    return dialogueResult;
                }

                index++;
                continue;
            }

            if (items[index] is TutorialPackageStepItem)
            {
                var steps = new List<ProductTourStep>();
                var startIndex = index;
                while (index < items.Count && items[index] is TutorialPackageStepItem stepItem)
                {
                    steps.Add(stepItem.Step);
                    index++;
                }

                _logger.LogInformation(
                    "Tutorial step group starting. PackageId={PackageId}, PackageItemIndex={PackageItemIndex}, StepCount={StepCount}",
                    packageId,
                    startIndex,
                    steps.Count);
                var stepResult = await RunStepsAsync(owner, steps, flowId, packageId, cancellationToken);
                if (stepResult != TutorialRunResult.Completed)
                {
                    return stepResult;
                }

                continue;
            }

            _logger.LogError(
                "Unsupported tutorial package item {ItemType}. PackageId={PackageId}",
                items[index].GetType().FullName,
                packageId);
            return TutorialRunResult.Failed;
        }

        return TutorialRunResult.Completed;
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
        while (index < steps.Count)
        {
            var step = steps[index];
            var preActionResult = await ExecuteStepActionsAsync(
                step.PreStepActions,
                owner,
                step,
                null,
                "pre-step",
                flowId,
                packageId,
                index,
                steps.Count,
                cancellationToken);
            if (preActionResult != TutorialRunResult.Completed)
            {
                return preActionResult;
            }

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

                _logger.LogInformation(
                    "Tutorial target lookup succeeded. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, TargetKey={TargetKey}, TargetType={TargetType}",
                    flowId,
                    packageId,
                    index,
                    steps.Count,
                    step.TargetKind,
                    step.TargetName,
                    step.TargetKey,
                    target.GetType().FullName);
            }

            _runObserver.OnStepShown(packageId ?? string.Empty, step.TargetName, step.Title);
            var overlay = new ProductTourOverlay(_textProvider, _options, _avatarProvider);
            _currentOverlay = overlay;
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
            try
            {
                using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                try
                {
                    var stepTask = overlay.ShowStepAsync(step, target, context, stepCts.Token);
                    var waitTask = WaitForStepSignalAsync(step, overlay, flowId, packageId, index, steps.Count, stepCts.Token);
                    var completedTask = await Task.WhenAny(stepTask, waitTask);
                    if (completedTask == waitTask)
                    {
                        await waitTask;
                    }

                    action = await stepTask;
                    _logger.LogInformation(
                        "Tutorial step action resolved. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, Action={Action}, CancellationRequested={CancellationRequested}",
                        flowId,
                        packageId,
                        index,
                        steps.Count,
                        step.TargetKind,
                        step.TargetName,
                        action,
                        cancellationToken.IsCancellationRequested);
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
                    _currentOverlay = null;
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
            catch (TimeoutException)
            {
                return TutorialRunResult.Failed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return TutorialRunResult.Canceled;
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

            if (action == ProductTourStepAction.ChildWindowHandoff)
            {
                return TutorialRunResult.ChildWindowHandoff;
            }

            var postActionResult = await ExecuteStepActionsAsync(
                step.PostStepActions,
                owner,
                step,
                target,
                "post-step",
                flowId,
                packageId,
                index,
                steps.Count,
                cancellationToken);
            if (postActionResult != TutorialRunResult.Completed)
            {
                return postActionResult;
            }

            index++;
        }

        return TutorialRunResult.Completed;
    }

    private async Task<TutorialRunResult> ExecuteStepActionsAsync(
        IEnumerable<TutorialStepAction> actions,
        FrameworkElement owner,
        ProductTourStep step,
        FrameworkElement? lastResolvedTarget,
        string phase,
        string? flowId,
        string? packageId,
        int stepIndex,
        int stepCount,
        CancellationToken cancellationToken)
    {
        var context = new TutorialStepActionContext
        {
            Services = _serviceProvider,
            Owner = owner,
            Step = step,
            LastResolvedTarget = lastResolvedTarget
        };

        foreach (var action in actions)
        {
            try
            {
                await action.ExecuteAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return TutorialRunResult.Canceled;
            }
            catch (Exception ex)
            {
                if (action.IsOptional)
                {
                    _logger.LogWarning(
                        ex,
                        "Optional tutorial {Phase} action {ActionName} failed. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, TargetKey={TargetKey}",
                        phase,
                        action.Name,
                        flowId,
                        packageId,
                        stepIndex,
                        stepCount,
                        step.TargetKind,
                        step.TargetName,
                        step.TargetKey);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Tutorial {Phase} action {ActionName} failed. FlowId={FlowId}, PackageId={PackageId}, StepIndex={StepIndex}, StepCount={StepCount}, TargetKind={TargetKind}, TargetName={TargetName}, TargetKey={TargetKey}",
                    phase,
                    action.Name,
                    flowId,
                    packageId,
                    stepIndex,
                    stepCount,
                    step.TargetKind,
                    step.TargetName,
                    step.TargetKey);
                return TutorialRunResult.Failed;
            }
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

        var signalTask = _signalService.WaitAsync(
            step.WaitForSignalId,
            null,
            Timeout.InfiniteTimeSpan,
            cancellationToken);
        var timeoutTask = Task.Delay(step.Timeout, cancellationToken);
        var completed = await Task.WhenAny(signalTask, timeoutTask);
        if (completed == timeoutTask)
        {
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
            throw new TimeoutException($"Tutorial signal '{step.WaitForSignalId}' timed out.");
        }

        await signalTask;
        await overlay.Dispatcher.InvokeAsync(
            overlay.MarkSignalCompleted,
            System.Windows.Threading.DispatcherPriority.Normal,
            cancellationToken);
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
            TutorialTargetKind.None => false,
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
        CancellationToken cancellationToken,
        TutorialCompletionKind includedPackageCompletionKind = TutorialCompletionKind.CoveredByFlow)
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
                    CompletionKind = includedPackageCompletionKind,
                    SourceFlowId = flow.FlowId
                };
            }
        }

        await _stateStore.SaveAsync(state, cancellationToken);
    }
}
