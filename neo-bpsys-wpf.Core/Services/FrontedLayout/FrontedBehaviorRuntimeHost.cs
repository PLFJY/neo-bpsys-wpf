using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Holds the runtime state for a single fronted window: the behavior document,
/// the event bus subscription, and all running behavior instances.
/// Disposing the host cancels all running behaviors and releases resources.
/// </summary>
internal sealed class FrontedBehaviorRuntimeHost : IDisposable
{
    private readonly FrontedBehaviorRuntimeContext _context;
    private readonly IFrontedEventBus _eventBus;
    private readonly IFrontedNodeGraphRuntime _graphRuntime;
    private readonly IFrontedAnimationRuntime _animationRuntime;
    private readonly FrontedBehaviorTriggerEvaluator _triggerEvaluator;
    private readonly FrontedNodeGraphValidator _graphValidator;
    private readonly ILogger _logger;
    private readonly Dictionary<Guid, RunningBehaviorState> _runningBehaviors = [];
    private readonly object _gate = new();

    private IDisposable? _eventSubscription;
    private FrontedBehaviorDocument? _document;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorRuntimeHost" />.
    /// </summary>
    public FrontedBehaviorRuntimeHost(
        FrontedBehaviorRuntimeContext context,
        IFrontedEventBus eventBus,
        IFrontedNodeGraphRuntime graphRuntime,
        IFrontedAnimationRuntime animationRuntime,
        FrontedBehaviorTriggerEvaluator triggerEvaluator)
    {
        _context = context;
        _eventBus = eventBus;
        _graphRuntime = graphRuntime;
        _animationRuntime = animationRuntime;
        _triggerEvaluator = triggerEvaluator;
        _graphValidator = new FrontedNodeGraphValidator();
        _logger = context.Logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <summary>
    /// Gets the context for this host.
    /// </summary>
    public FrontedBehaviorRuntimeContext Context => _context;

    /// <summary>
    /// Gets the loaded behavior document.
    /// </summary>
    public FrontedBehaviorDocument? Document => _document;

    /// <summary>
    /// Attaches this host: loads the behavior document and subscribes to the event bus.
    /// </summary>
    public async Task AttachAsync(FrontedBehaviorDocument document)
    {
        _document = document;
        // Subscribe to all events on the bus that are relevant to this window.
        _eventSubscription = _eventBus.Subscribe(null, OnEventAsync);

        _logger.LogInformation(
            "Behavior host attached: Window={WindowType}({WindowId}), BehaviorSets={SetCount}",
            _context.WindowType,
            _context.WindowId,
            _document.ControlBehaviorSets?.Count ?? 0);
    }

    /// <summary>
    /// Handles an incoming event from the event bus.
    /// </summary>
    private Task OnEventAsync(FrontedBehaviorEvent behaviorEvent)
    {
        if (_disposed || _document is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (!IsInScope(behaviorEvent))
            {
                return Task.CompletedTask;
            }

            ProcessEvent(behaviorEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Error processing event {EventType} on host {WindowId}.",
                behaviorEvent.EventType, _context.WindowId);
        }

        return Task.CompletedTask;
    }

    private bool IsInScope(FrontedBehaviorEvent behaviorEvent)
    {
        if (!string.IsNullOrEmpty(behaviorEvent.WindowId) &&
            !string.Equals(behaviorEvent.WindowId, _context.WindowId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(behaviorEvent.WindowType) &&
            !string.Equals(behaviorEvent.WindowType, _context.WindowType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(behaviorEvent.CanvasName) &&
            !string.Equals(behaviorEvent.CanvasName, FrontedLayoutConstants.BaseCanvasName, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private void ProcessEvent(FrontedBehaviorEvent behaviorEvent)
    {
        foreach (var set in _document!.ControlBehaviorSets)
        {
            if (set.BehaviorGuid == Guid.Empty)
            {
                continue;
            }

            foreach (var behavior in set.Behaviors)
            {
                if (!behavior.Enabled)
                {
                    continue;
                }

                ProcessBehavior(behavior, set, behaviorEvent);
            }
        }
    }

    private void ProcessBehavior(
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        FrontedBehaviorEvent behaviorEvent)
    {
        if (behavior.Kind == FrontedBehaviorKind.OneShot)
        {
            ProcessOneShot(behavior, set, behaviorEvent);
        }
        else if (behavior.Kind == FrontedBehaviorKind.Loop)
        {
            ProcessLoop(behavior, set, behaviorEvent);
        }
    }

    /// <summary>
    /// Creates runnable transition executions matching the supplied request.
    /// </summary>
    internal IReadOnlyList<FrontedTransitionExecution> CreateTransitionExecutions(
        FrontedTransitionRequest request,
        CancellationToken cancellationToken)
    {
        if (_disposed || _document is null)
        {
            return [];
        }

        var behaviorEvent = new FrontedBehaviorEvent
        {
            EventType = request.TransitionType,
            WindowType = request.WindowType,
            WindowId = _context.WindowId,
            CanvasName = FrontedLayoutConstants.BaseCanvasName,
            Source = "Transition",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = NormalizeTransitionPayload(request),
            IsPreview = _context.IsDesignerPreview
        };

        var executions = new List<FrontedTransitionExecution>();
        foreach (var set in _document.ControlBehaviorSets)
        {
            if (!MatchesTransitionTarget(set, request))
            {
                continue;
            }

            foreach (var behavior in set.Behaviors)
            {
                if (!behavior.Enabled || behavior.Kind != FrontedBehaviorKind.Transition)
                {
                    continue;
                }

                if (behavior.TransitionTrigger is null ||
                    !_triggerEvaluator.Evaluate(behavior.TransitionTrigger, behaviorEvent))
                {
                    continue;
                }

                if (TryCreateTransitionExecution(behavior, set, behaviorEvent, cancellationToken) is { } execution)
                {
                    executions.Add(execution);
                }
            }
        }

        return executions;
    }

    private FrontedTransitionExecution? TryCreateTransitionExecution(
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        FrontedBehaviorEvent behaviorEvent,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_runningBehaviors.TryGetValue(behavior.BehaviorId, out var existing))
            {
                switch (behavior.ReentryPolicy)
                {
                    case FrontedReentryPolicy.IgnoreIfRunning:
                        _logger.LogDebug("Transition {BehaviorId} ignored (already running).", behavior.BehaviorId);
                        return null;
                    case FrontedReentryPolicy.InterruptPrevious:
                        existing.LifecycleCts.Cancel();
                        _runningBehaviors.Remove(behavior.BehaviorId);
                        break;
                    case FrontedReentryPolicy.Queue:
                    case FrontedReentryPolicy.AllowParallel:
                        _logger.LogWarning(
                            "Transition {BehaviorId} reentry policy {Policy} is not implemented; ignoring while running.",
                            behavior.BehaviorId,
                            behavior.ReentryPolicy);
                        return null;
                }
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var state = new RunningBehaviorState(linkedCts);
            _runningBehaviors[behavior.BehaviorId] = state;
            return new FrontedTransitionExecution(this, behavior, set, behaviorEvent, state, linkedCts.Token);
        }
    }

    private static bool MatchesTransitionTarget(ControlBehaviorSet set, FrontedTransitionRequest request)
    {
        if (request.TargetBehaviorGuid != Guid.Empty)
        {
            return set.BehaviorGuid == request.TargetBehaviorGuid;
        }

        return !string.IsNullOrWhiteSpace(request.TargetDisplayName) &&
               string.Equals(set.DisplayName, request.TargetDisplayName, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, object?> NormalizeTransitionPayload(FrontedTransitionRequest request)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TransitionType"] = request.TransitionType,
            ["TargetBehaviorGuid"] = request.TargetBehaviorGuid,
            ["TargetDisplayName"] = request.TargetDisplayName
        };

        foreach (var item in request.Payload)
        {
            payload[item.Key] = item.Value;
            if (item.Key.StartsWith("Event.", StringComparison.Ordinal))
            {
                payload[item.Key["Event.".Length..]] = item.Value;
            }
        }

        return payload;
    }

    internal async Task RunTransitionGraphAsync(
        FrontedTransitionExecution execution,
        FrontedNodeGraph graph,
        string phase,
        CancellationToken cancellationToken)
    {
        try
        {
            var actionExecutor = CreateActionExecutor(execution.Set.BehaviorGuid, execution.Set.DisplayName);
            var graphContext = new FrontedGraphExecutionContext
            {
                BehaviorGuid = execution.Behavior.BehaviorId,
                CurrentControlDisplayName = execution.Set.DisplayName ?? string.Empty,
                TriggerEventType = execution.Event.EventType,
                EventPayload = execution.Event.Payload,
                ActionExecutor = actionExecutor
            };

            var result = await _graphRuntime.ExecuteAsync(graph, graphContext, cancellationToken);
            if (result.Status == FrontedGraphExecutionStatus.Success)
            {
                _logger.LogInformation(
                    "Transition {BehaviorId} {Phase} completed successfully.",
                    execution.Behavior.BehaviorId,
                    phase);
            }
            else if (result.Status == FrontedGraphExecutionStatus.Cancelled)
            {
                _logger.LogInformation(
                    "Transition {BehaviorId} {Phase} was cancelled.",
                    execution.Behavior.BehaviorId,
                    phase);
                execution.MarkCancellationRequested();
            }
            else
            {
                _logger.LogWarning(
                    "Transition {BehaviorId} {Phase} failed: {Reason}",
                    execution.Behavior.BehaviorId,
                    phase,
                    result.Exception?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            execution.MarkCancellationRequested();
            _logger.LogInformation(
                "Transition {BehaviorId} {Phase} execution cancelled.",
                execution.Behavior.BehaviorId,
                phase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Transition {BehaviorId} {Phase} threw; transition commit flow will continue when possible.",
                execution.Behavior.BehaviorId,
                phase);
        }
    }

    internal void CompleteTransition(FrontedTransitionExecution execution)
    {
        var shouldDispose = false;
        lock (_gate)
        {
            if (_runningBehaviors.TryGetValue(execution.Behavior.BehaviorId, out var current) &&
                ReferenceEquals(current, execution.State))
            {
                _runningBehaviors.Remove(execution.Behavior.BehaviorId);
                shouldDispose = true;
            }
            else if (!_runningBehaviors.ContainsValue(execution.State))
            {
                shouldDispose = true;
            }
        }

        if (shouldDispose)
        {
            execution.State.LifecycleCts.Dispose();
        }
    }

    private void ProcessOneShot(
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        FrontedBehaviorEvent behaviorEvent)
    {
        var trigger = behavior.Trigger;
        if (trigger is null)
        {
            return;
        }

        if (!_triggerEvaluator.Evaluate(trigger, behaviorEvent))
        {
            _logger.LogDebug(
                "OneShot {BehaviorId} trigger {EventType} did not match.",
                behavior.BehaviorId, trigger.EventType);
            return;
        }

        _logger.LogInformation(
            "OneShot {BehaviorId} triggered by {EventType}. Starting graph execution.",
            behavior.BehaviorId, trigger.EventType);

        lock (_gate)
        {
            if (_runningBehaviors.TryGetValue(behavior.BehaviorId, out var existing))
            {
                // Apply reentry policy
                switch (behavior.ReentryPolicy)
                {
                    case FrontedReentryPolicy.IgnoreIfRunning:
                        _logger.LogDebug("OneShot {BehaviorId} ignored (already running).", behavior.BehaviorId);
                        return;
                    case FrontedReentryPolicy.InterruptPrevious:
                        existing.LifecycleCts.Cancel();
                        _runningBehaviors.Remove(behavior.BehaviorId);
                        break;
                    case FrontedReentryPolicy.Queue:
                    case FrontedReentryPolicy.AllowParallel:
                        _logger.LogWarning(
                            "OneShot {BehaviorId} reentry policy {Policy} is not implemented in Phase 5 runtime; ignoring while running.",
                            behavior.BehaviorId,
                            behavior.ReentryPolicy);
                        return;
                    default:
                        break;
                }
            }
        }

        var cts = new CancellationTokenSource();
        var state = new RunningBehaviorState(cts);
        lock (_gate)
        {
            _runningBehaviors[behavior.BehaviorId] = state;
        }

        var task = ExecuteOneShotGraphAsync(behavior, set, behaviorEvent, state, cts.Token);
        state.RunningTask = task;
    }

    private async Task ExecuteOneShotGraphAsync(
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        FrontedBehaviorEvent behaviorEvent,
        RunningBehaviorState state,
        CancellationToken cancellationToken)
    {
        try
        {
            var actionExecutor = CreateActionExecutor(set.BehaviorGuid, set.DisplayName);
            var context = new FrontedGraphExecutionContext
            {
                BehaviorGuid = behavior.BehaviorId,
                CurrentControlDisplayName = set.DisplayName ?? string.Empty,
                TriggerEventType = behaviorEvent.EventType,
                EventPayload = behaviorEvent.Payload,
                ActionExecutor = actionExecutor
            };

            var result = await _graphRuntime.ExecuteAsync(behavior.Graph, context, cancellationToken);

            if (result.Status == FrontedGraphExecutionStatus.Success)
            {
                _logger.LogInformation("OneShot {BehaviorId} completed successfully.", behavior.BehaviorId);
            }
            else if (result.Status == FrontedGraphExecutionStatus.Cancelled)
            {
                _logger.LogInformation("OneShot {BehaviorId} was cancelled.", behavior.BehaviorId);
            }
            else
            {
                _logger.LogWarning("OneShot {BehaviorId} failed: {Reason}", behavior.BehaviorId, result.Exception?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OneShot {BehaviorId} execution cancelled.", behavior.BehaviorId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OneShot {BehaviorId} threw an exception.", behavior.BehaviorId);
        }
        finally
        {
            var shouldDispose = false;
            lock (_gate)
            {
                if (_runningBehaviors.TryGetValue(behavior.BehaviorId, out var current) &&
                    ReferenceEquals(current, state))
                {
                    _runningBehaviors.Remove(behavior.BehaviorId);
                    shouldDispose = true;
                }
                else if (!_runningBehaviors.ContainsValue(state))
                {
                    shouldDispose = true;
                }
            }

            if (shouldDispose)
            {
                state.LifecycleCts.Dispose();
            }
        }
    }

    private void ProcessLoop(
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        FrontedBehaviorEvent behaviorEvent)
    {
        lock (_gate)
        {
            var startTrigger = behavior.StartTrigger;
            var startMatches = startTrigger is not null &&
                _triggerEvaluator.Evaluate(startTrigger, behaviorEvent);

            if (!_runningBehaviors.TryGetValue(behavior.BehaviorId, out var state))
            {
                // Stopped state - check StartTrigger
                if (!startMatches || startTrigger is null)
                {
                    return;
                }

                _logger.LogInformation(
                    "Loop {BehaviorId} starting via StartTrigger {EventType}.",
                    behavior.BehaviorId, startTrigger.EventType);

                var cts = new CancellationTokenSource();
                state = new RunningBehaviorState(cts, LoopPhase.Starting, behavior, set, behaviorEvent);
                _runningBehaviors[behavior.BehaviorId] = state;

                state.RunningTask = ExecuteLoopLifecycleAsync(behavior, set, state, cts.Token);
                _logger.LogInformation(
                    "Loop started. BehaviorId={BehaviorId}, BehaviorGuid={BehaviorGuid}, Window={WindowType}, DisplayName={DisplayName}",
                    behavior.BehaviorId,
                    set.BehaviorGuid,
                    _context.WindowType,
                    set.DisplayName);
                return;
            }

            if (startMatches)
            {
                switch (behavior.LoopPolicy?.ReentryPolicy ?? FrontedReentryPolicy.IgnoreIfRunning)
                {
                    case FrontedReentryPolicy.IgnoreIfRunning:
                        _logger.LogDebug("Loop {BehaviorId} ignored (already running).", behavior.BehaviorId);
                        return;
                    case FrontedReentryPolicy.InterruptPrevious:
                        state.LifecycleCts.Cancel();
                        _runningBehaviors.Remove(behavior.BehaviorId);
                        var cts = new CancellationTokenSource();
                        var nextState = new RunningBehaviorState(cts, LoopPhase.Starting, behavior, set, behaviorEvent);
                        _runningBehaviors[behavior.BehaviorId] = nextState;
                        nextState.RunningTask = ExecuteLoopLifecycleAsync(behavior, set, nextState, cts.Token);
                        return;
                    case FrontedReentryPolicy.Queue:
                    case FrontedReentryPolicy.AllowParallel:
                        _logger.LogWarning(
                            "Loop {BehaviorId} reentry policy {Policy} is not implemented in Phase 5 runtime; ignoring while running.",
                            behavior.BehaviorId,
                            behavior.LoopPolicy?.ReentryPolicy);
                        return;
                }
            }

            // Running state - check StopTriggers. Filters inside one trigger are AND;
            // triggers in the list are OR.
            if (state.LoopPhase is LoopPhase.Starting or LoopPhase.Looping)
            {
                var stopTrigger = behavior.StopTriggers.FirstOrDefault(trigger =>
                    _triggerEvaluator.Evaluate(trigger, behaviorEvent));
                if (stopTrigger is null)
                {
                    return;
                }

                _logger.LogInformation(
                    "Loop stop trigger matched. BehaviorId={BehaviorId}, EventType={EventType}.",
                    behavior.BehaviorId, stopTrigger.EventType);
                RequestLoopStop(
                    behavior,
                    state,
                    GetStopReason(behaviorEvent),
                    forceRunStopGraph: false,
                    stopEvent: behaviorEvent);
            }
        }
    }

    private static FrontedBehaviorStopReason GetStopReason(FrontedBehaviorEvent behaviorEvent) =>
        behaviorEvent.EventType switch
        {
            "Guidance.Cancelled" => FrontedBehaviorStopReason.GuidanceCancelled,
            "Guidance.Stopped" => FrontedBehaviorStopReason.GuidanceStopped,
            "WindowHidden" => FrontedBehaviorStopReason.WindowHidden,
            _ => FrontedBehaviorStopReason.ManualClear
        };

    private void RequestLoopStop(
        FrontedBehavior behavior,
        RunningBehaviorState state,
        FrontedBehaviorStopReason reason,
        bool forceRunStopGraph,
        FrontedBehaviorEvent? stopEvent = null)
    {
        state.StopRequested = true;
        state.StopReason = reason;
        state.StopEvent = stopEvent;
        var stopMode = forceRunStopGraph
            ? FrontedLoopStopMode.RunStopGraph
            : behavior.LoopPolicy?.StopMode ?? FrontedLoopStopMode.StopImmediately;
        state.RequestedStopMode = stopMode;

        _logger.LogInformation(
            "Loop stopping reason. BehaviorId={BehaviorId}, Reason={Reason}, StopMode={StopMode}.",
            behavior.BehaviorId,
            reason,
            stopMode);

        if (state.LoopPhase == LoopPhase.Starting)
        {
            switch (stopMode)
            {
                case FrontedLoopStopMode.RunStopGraph:
                case FrontedLoopStopMode.StopImmediately:
                    state.StartCts.Cancel();
                    _logger.LogDebug("Loop {BehaviorId} StartGraph cancellation requested.", behavior.BehaviorId);
                    break;
                case FrontedLoopStopMode.CompleteCurrentIteration:
                    break;
            }
        }
        else if (state.LoopPhase == LoopPhase.Looping)
        {
            switch (stopMode)
            {
                case FrontedLoopStopMode.RunStopGraph:
                case FrontedLoopStopMode.StopImmediately:
                    state.LoopCts.Cancel();
                    _logger.LogDebug("Loop {BehaviorId} LoopGraph cancellation requested.", behavior.BehaviorId);
                    break;
                case FrontedLoopStopMode.CompleteCurrentIteration:
                case FrontedLoopStopMode.HoldCurrentState:
                    break;
            }
        }
    }

    private async Task ExecuteLoopLifecycleAsync(
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        RunningBehaviorState state,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                // ═══════════════════════════════════════════════
                // Phase 1: StartGraph
                // Uses linked token from LifecycleCts + StartCts.
                // LifecycleCts: cancelled by Dispose/Detach/InterruptPrevious.
                // StartCts: cancelled by StopTriggers during Starting for RunStopGraph/StopImmediately.
                // ═══════════════════════════════════════════════
                var startExecutor = CreateActionExecutor(set.BehaviorGuid, set.DisplayName);
                var startContext = new FrontedGraphExecutionContext
                {
                    BehaviorGuid = behavior.BehaviorId,
                    CurrentControlDisplayName = set.DisplayName ?? string.Empty,
                    TriggerEventType = state.StartEvent?.EventType ?? string.Empty,
                    EventPayload = state.StartEvent?.Payload ?? new Dictionary<string, object?>(),
                    StartEventPayload = state.StartEvent?.Payload ?? new Dictionary<string, object?>(),
                    ActionExecutor = startExecutor
                };

                using (var startLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, state.StartCts.Token))
                {
                    await _graphRuntime.ExecuteAsync(behavior.StartGraph, startContext, startLinkedCts.Token);
                }

                // ═══════════════════════════════════════════════
                // Phase 2: LoopGraph (repeating)
                // Uses LoopCts.Token — cancelled by StopTriggers for RunStopGraph/StopImmediately.
                // CompleteCurrentIteration and HoldCurrentState do NOT cancel.
                // ═══════════════════════════════════════════════
                state.LoopPhase = LoopPhase.Looping;

                if (!state.StopRequested)
                {
                    var repeatCount = behavior.LoopPolicy?.RepeatCount ?? -1;
                    var intervalMs = Math.Max(0, behavior.LoopPolicy?.IntervalMs ?? 0);
                    var iteration = 0;
                    var loopCt = state.LoopCts.Token;

                    while (repeatCount == -1 || iteration < repeatCount)
                    {
                        // StopTriggers (RunStopGraph/StopImmediately) cancel LoopCts.
                        loopCt.ThrowIfCancellationRequested();

                        if (intervalMs > 0 && iteration > 0)
                        {
                            await Task.Delay(intervalMs, loopCt);
                        }

                        var loopExecutor = CreateActionExecutor(set.BehaviorGuid, set.DisplayName);
                        var loopContext = new FrontedGraphExecutionContext
                        {
                            BehaviorGuid = behavior.BehaviorId,
                            CurrentControlDisplayName = set.DisplayName ?? string.Empty,
                            TriggerEventType = state.StartEvent?.EventType ?? string.Empty,
                            EventPayload = state.StartEvent?.Payload ?? new Dictionary<string, object?>(),
                            StartEventPayload = state.StartEvent?.Payload ?? new Dictionary<string, object?>(),
                            ActionExecutor = loopExecutor
                        };

                        await _graphRuntime.ExecuteAsync(behavior.LoopGraph, loopContext, loopCt);
                        iteration++;

                        // CompleteCurrentIteration / HoldCurrentState: check StopRequested after each iteration
                        if (state.StopRequested)
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!state.StopRequested)
            {
                // Lifecycle CTS cancelled (Dispose/Detach/InterruptPrevious) → re-throw to outer catch
                throw;
            }
            catch (OperationCanceledException) when (state.StopRequested)
            {
                // StopTriggers cancelled StartGraph or LoopGraph — swallow and proceed to Phase 3
                _logger.LogDebug("Loop {BehaviorId} StartGraph/LoopGraph cancelled by stop trigger.", behavior.BehaviorId);
            }

            // ═══════════════════════════════════════════════
            // Phase 3: StopGraph
            // Runs only when StopRequested and mode requests it.
            // Uses CancellationToken.None — never cancelled, always plays to completion.
            // Executes after Phase 1+2 regardless of whether they were cancelled by StopTriggers.
            // ═══════════════════════════════════════════════
            state.LoopPhase = LoopPhase.Stopping;

            if (state.StopRequested)
            {
                await Dispatcher.Yield(DispatcherPriority.Render);
                switch (state.RequestedStopMode)
                {
                    case FrontedLoopStopMode.RunStopGraph:
                    case FrontedLoopStopMode.CompleteCurrentIteration:
                        if (behavior.StopGraph is null)
                        {
                            _logger.LogWarning(
                                "Loop {BehaviorId} StopGraph is null — skipping Phase 3.",
                                behavior.BehaviorId);
                            break;
                        }

                        // Validate StopGraph before execution; if invalid, warn and suppress reset
                        // to avoid silently snapping properties back to original values.
                        var stopValidation = _graphValidator?.Validate(behavior.StopGraph) ?? [];
                        var hasStopGraphError = stopValidation.Any(m => m.Severity == FrontedNodeGraphValidationSeverity.Error);
                        var hasNoStartNode = !behavior.StopGraph.Nodes.Any(n => n.NodeType == "flow.start");
                        if (hasStopGraphError || hasNoStartNode)
                        {
                            _logger.LogWarning(
                                "Loop {BehaviorId} StopGraph validation has issues — suppressing reset to avoid masking. " +
                                "Errors={ErrorCount}, HasStartNode={HasStartNode}",
                                behavior.BehaviorId,
                                stopValidation.Count(m => m.Severity == FrontedNodeGraphValidationSeverity.Error),
                                !hasNoStartNode);

                            if (hasNoStartNode)
                            {
                                _logger.LogWarning(
                                    "Loop {BehaviorId} StopGraph has no Start node; insert a Start node to enable StopGraph execution.",
                                    behavior.BehaviorId);
                            }

                            state.SuppressReset = true;
                            // Fall through to fire-and-forget check and ExecuteAsync rather than skipping execution entirely,
                            // because the graph runtime may still handle partial StopGraphs gracefully.
                        }

                        // Detect fire-and-forget animations in StopGraph and warn the user.
                        var fireAndForgetNodes = behavior.StopGraph.Nodes
                            .Where(n => n.NodeType == "action.animateProperty"
                                        && GetBoolSafe(n, "WaitForCompletion") == false)
                            .ToArray();
                        if (fireAndForgetNodes.Length > 0)
                        {
                            _logger.LogWarning(
                                "Loop {BehaviorId} StopGraph contains {Count} animateProperty node(s) with WaitForCompletion=false. " +
                                "These fire-and-forget animations may be immediately overridden by ResetIfNeeded.",
                                behavior.BehaviorId,
                                fireAndForgetNodes.Length);
                        }

                        var stopExecutor = CreateActionExecutor(set.BehaviorGuid, set.DisplayName);
                        var stopContext = new FrontedGraphExecutionContext
                        {
                            BehaviorGuid = behavior.BehaviorId,
                            CurrentControlDisplayName = set.DisplayName ?? string.Empty,
                            TriggerEventType = state.StopEvent?.EventType ?? string.Empty,
                            EventPayload = state.StopEvent?.Payload ?? new Dictionary<string, object?>(),
                            StartEventPayload = state.StartEvent?.Payload ?? new Dictionary<string, object?>(),
                            StopEventPayload = state.StopEvent?.Payload ?? new Dictionary<string, object?>(),
                            ActionExecutor = stopExecutor
                        };

                        _logger.LogInformation("Loop {BehaviorId} StopGraph started.", behavior.BehaviorId);
                        var stopResult = await _graphRuntime.ExecuteAsync(
                            behavior.StopGraph, stopContext, state.StopGraphCts.Token);
                        foreach (var item in stopResult.LogItems)
                        {
                            _logger.LogDebug(
                                "Loop {BehaviorId} StopGraph trace: Node={NodeId}, Level={Level}, Message={Message}",
                                behavior.BehaviorId,
                                item.NodeId,
                                item.Level,
                                item.Message);
                        }

                        if (stopResult.Status != FrontedGraphExecutionStatus.Success)
                        {
                            _logger.LogWarning(
                                "Loop {BehaviorId} StopGraph did not complete successfully (Status={Status}); suppressing reset.",
                                behavior.BehaviorId, stopResult.Status);
                            state.SuppressReset = true;
                        }
                        else
                        {
                            // StopGraph completed successfully — suppress reset so its visual result is not overridden.
                            state.SuppressReset = true;
                            _logger.LogInformation("Loop {BehaviorId} StopGraph completed.", behavior.BehaviorId);
                        }
                        break;
                    case FrontedLoopStopMode.StopImmediately:
                    case FrontedLoopStopMode.HoldCurrentState:
                        // No StopGraph in these modes
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (!state.StopRequested)
        {
            // Lifecycle CTS cancelled (Dispose/Detach/InterruptPrevious) — exit immediately
            _logger.LogInformation("Loop {BehaviorId} lifecycle cancelled.", behavior.BehaviorId);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Loop {BehaviorId} lifecycle threw.", behavior.BehaviorId);
        }
        finally
        {
            // ═══════════════════════════════════════════════
            // Phase 4: Reset (unless HoldCurrentState or SuppressReset)
            // SuppressReset is set when StopGraph executed successfully or failed validation,
            // to prevent Reset from overriding the StopGraph visual result.
            // ═══════════════════════════════════════════════
            if (state.RequestedStopMode != FrontedLoopStopMode.HoldCurrentState
                && !state.SuppressReset)
            {
                _logger.LogDebug("Loop {BehaviorId} ResetIfNeeded executing.", behavior.BehaviorId);
                ResetIfNeeded(behavior, set);
            }
            else
            {
                _logger.LogDebug(
                    "Loop {BehaviorId} ResetIfNeeded skipped. StopMode={StopMode}, SuppressReset={SuppressReset}.",
                    behavior.BehaviorId,
                    state.RequestedStopMode,
                    state.SuppressReset);
            }

            // ═══════════════════════════════════════════════
            // Phase 5: Cleanup (always, exactly once)
            // ═══════════════════════════════════════════════
            CleanupLoop(behavior, state);
        }
    }

    private void ResetIfNeeded(FrontedBehavior behavior, ControlBehaviorSet set)
    {
        if (behavior.LoopPolicy?.ResetOnStop != true)
        {
            return;
        }

        var resetContext = new FrontedAnimationExecutionContext
        {
            Root = _context.RootCanvas,
            SelfBehaviorGuid = set.BehaviorGuid,
            SelfDisplayName = set.DisplayName,
            WindowId = _context.WindowId,
            CanvasName = _context.CanvasName,
            IsDesignerPreview = false,
            Logger = _logger
        };
        _animationRuntime.ResetTarget(set.BehaviorGuid, resetContext);
    }

    private void CleanupLoop(FrontedBehavior behavior, RunningBehaviorState state)
    {
        var shouldDispose = false;
        lock (_gate)
        {
            if (_runningBehaviors.TryGetValue(behavior.BehaviorId, out var current) &&
                ReferenceEquals(current, state))
            {
                _runningBehaviors.Remove(behavior.BehaviorId);
                shouldDispose = true;
            }
            else if (!_runningBehaviors.ContainsValue(state))
            {
                shouldDispose = true;
            }
        }

        if (shouldDispose)
        {
            state.LifecycleCts.Dispose();
            state.LoopCts.Dispose();
            state.StartCts.Dispose();
            state.StopGraphCts.Dispose();
        }
    }

    /// <summary>
    /// Cancels all running behaviors for this host.
    /// </summary>
    public void CancelAllRunningBehaviors()
    {
        List<RunningBehaviorState> states;
        lock (_gate)
        {
            states = [.. _runningBehaviors.Values];
            _runningBehaviors.Clear();
        }

        foreach (var state in states)
        {
            try
            {
                state.LifecycleCts.Cancel();
                state.LoopCts.Cancel();
                state.StartCts.Cancel();
                state.LifecycleCts.Dispose();
                state.LoopCts.Dispose();
                state.StartCts.Dispose();
                state.StopGraphCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// Stops all active loop behaviors in this host and waits up to the supplied timeout for cleanup.
    /// </summary>
    /// <param name="reason">The reason for stopping loops.</param>
    /// <param name="timeout">Maximum time to wait for each loop StopGraph.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of loops requested to stop.</returns>
    public async Task<int> StopAllLoopBehaviorsAsync(
        FrontedBehaviorStopReason reason,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        List<RunningBehaviorState> states;
        lock (_gate)
        {
            states = _runningBehaviors.Values
                .Where(state => state.Behavior?.Kind == FrontedBehaviorKind.Loop)
                .ToList();

            foreach (var state in states)
            {
                RequestLoopStop(state.Behavior!, state, reason, forceRunStopGraph: true);
            }
        }

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.RunningTask is null)
            {
                ForceClearLoop(state, reason);
                continue;
            }

            try
            {
                await state.RunningTask.WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "Loop StopGraph timed out. BehaviorId={BehaviorId}, Reason={Reason}, TimeoutMs={TimeoutMs}",
                    state.Behavior?.BehaviorId,
                    reason,
                    timeout.TotalMilliseconds);
                try
                {
                    state.StopGraphCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                ForceClearLoop(state, reason);
            }
        }

        return states.Count;
    }

    private void ForceClearLoop(RunningBehaviorState state, FrontedBehaviorStopReason reason)
    {
        if (state.Behavior is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_runningBehaviors.TryGetValue(state.Behavior.BehaviorId, out var current) &&
                ReferenceEquals(current, state))
            {
                _runningBehaviors.Remove(state.Behavior.BehaviorId);
            }
        }

        _logger.LogWarning(
            "Loop force cleared. BehaviorId={BehaviorId}, Reason={Reason}",
            state.Behavior.BehaviorId,
            reason);
    }

    private FrontedAnimationRuntimeActionExecutor CreateActionExecutor(Guid behaviorGuid, string? displayName)
    {
        return new FrontedAnimationRuntimeActionExecutor(
            _animationRuntime,
            _context.RootCanvas,
            behaviorGuid,
            displayName,
            _context.WindowId,
            _context.CanvasName,
            _logger);
    }

    /// <summary>
    /// Reads a boolean property from a <see cref="FrontedNode"/>'s properties dict,
    /// returning <paramref name="fallback"/> if the property is missing or not a valid boolean.
    /// </summary>
    private static bool GetBoolSafe(FrontedNode node, string name, bool fallback = true)
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var result) ? result : fallback;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelAllRunningBehaviors();
        _eventSubscription?.Dispose();
        _animationRuntime.Release(_context.RootCanvas);

        _logger.LogInformation(
            "Behavior host detached: Window={WindowType}({WindowId}), Canvas={CanvasName}",
            _context.WindowType,
            _context.WindowId,
            _context.CanvasName);
    }

    internal enum LoopPhase
    {
        Stopped,
        Starting,
        Looping,
        Stopping
    }

    internal sealed class RunningBehaviorState
    {
        /// <summary>
        /// CTS for the entire lifecycle. Cancelled only by Dispose/Detach/InterruptPrevious.
        /// Never cancelled by stop triggers.
        /// </summary>
        public CancellationTokenSource LifecycleCts { get; }

        /// <summary>
        /// CTS for cancelling the LoopGraph iteration only.
        /// Cancelled by stop triggers during Looping phase for RunStopGraph/StopImmediately.
        /// </summary>
        public CancellationTokenSource LoopCts { get; } = new();

        /// <summary>
        /// CTS for cancelling StartGraph only.
        /// Cancelled by stop triggers during Starting phase for RunStopGraph/StopImmediately.
        /// </summary>
        public CancellationTokenSource StartCts { get; } = new();

        /// <summary>
        /// CTS for cancelling StopGraph when a bounded external stop operation times out.
        /// </summary>
        public CancellationTokenSource StopGraphCts { get; } = new();

        public LoopPhase LoopPhase { get; set; }
        public Task? RunningTask { get; set; }
        public bool StopRequested { get; set; }
        public FrontedLoopStopMode? RequestedStopMode { get; set; }
        public FrontedBehaviorStopReason? StopReason { get; set; }
        public FrontedBehavior? Behavior { get; }
        public ControlBehaviorSet? Set { get; }

        /// <summary>
        /// Gets the event that matched the loop start trigger.
        /// </summary>
        public FrontedBehaviorEvent? StartEvent { get; }

        /// <summary>
        /// Gets or sets the event that matched a loop stop trigger.
        /// </summary>
        public FrontedBehaviorEvent? StopEvent { get; set; }

        /// <summary>
        /// When true, Phase 4 Reset is skipped. Set when StopGraph executed successfully
        /// or failed validation, to prevent Reset from overriding the StopGraph visual result.
        /// </summary>
        public bool SuppressReset { get; set; }

        public RunningBehaviorState(
            CancellationTokenSource lifecycleCts,
            LoopPhase loopPhase = LoopPhase.Stopped,
            FrontedBehavior? behavior = null,
            ControlBehaviorSet? set = null,
            FrontedBehaviorEvent? startEvent = null)
        {
            LifecycleCts = lifecycleCts;
            LoopPhase = loopPhase;
            Behavior = behavior;
            Set = set;
            StartEvent = startEvent;
        }
    }
}

/// <summary>
/// Runtime handle for one matched transition behavior execution.
/// </summary>
internal sealed class FrontedTransitionExecution
{
    private readonly FrontedBehaviorRuntimeHost _host;
    private readonly CancellationToken _lifecycleToken;
    private bool _isCancellationRequested;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedTransitionExecution"/>.
    /// </summary>
    public FrontedTransitionExecution(
        FrontedBehaviorRuntimeHost host,
        FrontedBehavior behavior,
        ControlBehaviorSet set,
        FrontedBehaviorEvent behaviorEvent,
        FrontedBehaviorRuntimeHost.RunningBehaviorState state,
        CancellationToken lifecycleToken)
    {
        _host = host;
        Behavior = behavior;
        Set = set;
        Event = behaviorEvent;
        State = state;
        _lifecycleToken = lifecycleToken;
    }

    /// <summary>
    /// Gets the matched behavior.
    /// </summary>
    public FrontedBehavior Behavior { get; }

    /// <summary>
    /// Gets the matched control behavior set.
    /// </summary>
    public ControlBehaviorSet Set { get; }

    /// <summary>
    /// Gets the synthetic transition event used for trigger and graph payload context.
    /// </summary>
    public FrontedBehaviorEvent Event { get; }

    /// <summary>
    /// Gets the running state reserved for this transition.
    /// </summary>
    public FrontedBehaviorRuntimeHost.RunningBehaviorState State { get; }

    /// <summary>
    /// Gets whether this transition was interrupted or cancelled.
    /// </summary>
    public bool IsCancellationRequested => _isCancellationRequested || _lifecycleToken.IsCancellationRequested;

    /// <summary>
    /// Marks this execution as cancelled.
    /// </summary>
    public void MarkCancellationRequested()
    {
        _isCancellationRequested = true;
    }

    /// <summary>
    /// Runs the exit graph.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the exit graph finishes or is suppressed.</returns>
    public Task RunExitGraphAsync(CancellationToken cancellationToken) =>
        _host.RunTransitionGraphAsync(this, Behavior.ExitGraph, "ExitGraph", Link(cancellationToken));

    /// <summary>
    /// Runs the enter graph.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the enter graph finishes or is suppressed.</returns>
    public Task RunEnterGraphAsync(CancellationToken cancellationToken) =>
        IsCancellationRequested
            ? Task.CompletedTask
            : _host.RunTransitionGraphAsync(this, Behavior.EnterGraph, "EnterGraph", Link(cancellationToken));

    /// <summary>
    /// Completes this transition and releases its reserved running state.
    /// </summary>
    public void Complete()
    {
        _host.CompleteTransition(this);
    }

    private CancellationToken Link(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return _lifecycleToken;
        }

        if (!_lifecycleToken.CanBeCanceled)
        {
            return cancellationToken;
        }

        // The lifecycle token already links the original orchestration token. Return it to avoid
        // allocating nested token sources for every graph phase.
        return _lifecycleToken;
    }
}
