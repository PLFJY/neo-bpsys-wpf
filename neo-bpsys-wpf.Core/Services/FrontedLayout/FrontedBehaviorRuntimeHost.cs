using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Holds the runtime state for a single Canvas: the behavior document,
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
    private readonly ILogger _logger;
    private readonly Dictionary<Guid, RunningBehaviorState> _runningBehaviors = [];
    private readonly Dictionary<Guid, IReadOnlyDictionary<string, string>> _selfTagsByBehaviorGuid = [];
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
        BuildSelfTagIndex();

        // Subscribe to all events on the bus that are relevant to this window/canvas
        _eventSubscription = _eventBus.Subscribe(null, OnEventAsync);

        _logger.LogInformation(
            "Behavior host attached: Window={WindowType}({WindowId}), Canvas={CanvasName}, BehaviorSets={SetCount}",
            _context.WindowType,
            _context.WindowId,
            _context.CanvasName,
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
                "Error processing event {EventType} on host {WindowId}/{CanvasName}.",
                behaviorEvent.EventType, _context.WindowId, _context.CanvasName);
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
            !string.Equals(behaviorEvent.CanvasName, _context.CanvasName, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private void BuildSelfTagIndex()
    {
        _selfTagsByBehaviorGuid.Clear();
        foreach (var control in _context.CanvasConfig.Controls.Values)
        {
            if (control.BehaviorGuid == Guid.Empty || control.BehaviorTags.Count == 0)
            {
                continue;
            }

            _selfTagsByBehaviorGuid[control.BehaviorGuid] = control.BehaviorTags;
        }
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

        var selfTags = GetSelfTags(set);
        if (!_triggerEvaluator.Evaluate(trigger, behaviorEvent, selfTags))
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
                SelfTags = GetSelfTagsAsObjects(set),
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
                _triggerEvaluator.Evaluate(startTrigger, behaviorEvent, GetSelfTags(set));

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
                state = new RunningBehaviorState(cts, LoopPhase.Starting);
                _runningBehaviors[behavior.BehaviorId] = state;

                state.RunningTask = ExecuteLoopLifecycleAsync(behavior, set, state, cts.Token);
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
                        var nextState = new RunningBehaviorState(cts, LoopPhase.Starting);
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

            // Running state - check EndTrigger
            if (state.LoopPhase is LoopPhase.Starting or LoopPhase.Looping)
            {
                var endTrigger = behavior.EndTrigger;
                if (endTrigger is null)
                {
                    return;
                }

                if (!_triggerEvaluator.Evaluate(endTrigger, behaviorEvent, GetSelfTags(set)))
                {
                    return;
                }

                _logger.LogInformation(
                    "Loop {BehaviorId} stopping via EndTrigger {EventType}.",
                    behavior.BehaviorId, endTrigger.EventType);

                state.StopRequested = true;
                var stopMode = behavior.LoopPolicy?.StopMode ?? FrontedLoopStopMode.StopImmediately;
                state.RequestedStopMode = stopMode;

                if (state.LoopPhase == LoopPhase.Starting)
                {
                    // Starting phase: cancel StartGraph for RunStopGraph/StopImmediately
                    switch (stopMode)
                    {
                        case FrontedLoopStopMode.RunStopGraph:
                        case FrontedLoopStopMode.StopImmediately:
                            state.StartCts.Cancel();
                            break;
                        case FrontedLoopStopMode.CompleteCurrentIteration:
                            // Don't cancel — let StartGraph complete, then StopGraph
                            break;
                    }
                }
                else // Looping phase
                {
                    // Looping phase: cancel LoopGraph for RunStopGraph/StopImmediately
                    switch (stopMode)
                    {
                        case FrontedLoopStopMode.RunStopGraph:
                        case FrontedLoopStopMode.StopImmediately:
                            state.LoopCts.Cancel();
                            break;
                        case FrontedLoopStopMode.CompleteCurrentIteration:
                        case FrontedLoopStopMode.HoldCurrentState:
                            // Don't cancel — let current iteration finish or hold state
                            break;
                    }
                }
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
                // StartCts: cancelled by EndTrigger during Starting for RunStopGraph/StopImmediately.
                // ═══════════════════════════════════════════════
                var startExecutor = CreateActionExecutor(set.BehaviorGuid, set.DisplayName);
                var startContext = new FrontedGraphExecutionContext
                {
                    BehaviorGuid = behavior.BehaviorId,
                    CurrentControlDisplayName = set.DisplayName ?? string.Empty,
                    SelfTags = GetSelfTagsAsObjects(set),
                    ActionExecutor = startExecutor
                };

                using (var startLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, state.StartCts.Token))
                {
                    await _graphRuntime.ExecuteAsync(behavior.StartGraph, startContext, startLinkedCts.Token);
                }

                // ═══════════════════════════════════════════════
                // Phase 2: LoopGraph (repeating)
                // Uses LoopCts.Token — cancelled by EndTrigger for RunStopGraph/StopImmediately.
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
                        // EndTrigger (RunStopGraph/StopImmediately) cancels LoopCts → throw here
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
                            SelfTags = GetSelfTagsAsObjects(set),
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
                // EndTrigger cancelled StartGraph or LoopGraph — swallow and proceed to Phase 3
                _logger.LogDebug("Loop {BehaviorId} StartGraph/LoopGraph cancelled by EndTrigger.", behavior.BehaviorId);
            }

            // ═══════════════════════════════════════════════
            // Phase 3: StopGraph
            // Runs only when StopRequested and mode requests it.
            // Uses CancellationToken.None — never cancelled, always plays to completion.
            // Executes after Phase 1+2 regardless of whether they were cancelled by EndTrigger.
            // ═══════════════════════════════════════════════
            state.LoopPhase = LoopPhase.Stopping;

            if (state.StopRequested)
            {
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

                        var stopExecutor = CreateActionExecutor(set.BehaviorGuid, set.DisplayName);
                        var stopContext = new FrontedGraphExecutionContext
                        {
                            BehaviorGuid = behavior.BehaviorId,
                            CurrentControlDisplayName = set.DisplayName ?? string.Empty,
                            SelfTags = GetSelfTagsAsObjects(set),
                            ActionExecutor = stopExecutor
                        };

                        var stopResult = await _graphRuntime.ExecuteAsync(
                            behavior.StopGraph, stopContext, CancellationToken.None);

                        if (stopResult.Status != FrontedGraphExecutionStatus.Success)
                        {
                            _logger.LogWarning(
                                "Loop {BehaviorId} StopGraph did not complete successfully (Status={Status}).",
                                behavior.BehaviorId, stopResult.Status);
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
            // Phase 4: Reset (unless HoldCurrentState)
            // ═══════════════════════════════════════════════
            if (state.RequestedStopMode != FrontedLoopStopMode.HoldCurrentState)
            {
                ResetIfNeeded(behavior, set);
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
            }
            catch (ObjectDisposedException)
            {
            }
        }
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

    private IReadOnlyDictionary<string, string> GetSelfTags(ControlBehaviorSet set) =>
        _selfTagsByBehaviorGuid.GetValueOrDefault(set.BehaviorGuid) ?? new Dictionary<string, string>();

    private IReadOnlyDictionary<string, object?> GetSelfTagsAsObjects(ControlBehaviorSet set) =>
        GetSelfTags(set).ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);

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
        /// Never cancelled by EndTrigger.
        /// </summary>
        public CancellationTokenSource LifecycleCts { get; }

        /// <summary>
        /// CTS for cancelling the LoopGraph iteration only.
        /// Cancelled by EndTrigger during Looping phase for RunStopGraph/StopImmediately.
        /// </summary>
        public CancellationTokenSource LoopCts { get; } = new();

        /// <summary>
        /// CTS for cancelling StartGraph only.
        /// Cancelled by EndTrigger during Starting phase for RunStopGraph/StopImmediately.
        /// </summary>
        public CancellationTokenSource StartCts { get; } = new();

        public LoopPhase LoopPhase { get; set; }
        public Task? RunningTask { get; set; }
        public bool StopRequested { get; set; }
        public FrontedLoopStopMode? RequestedStopMode { get; set; }

        public RunningBehaviorState(CancellationTokenSource lifecycleCts, LoopPhase loopPhase = LoopPhase.Stopped)
        {
            LifecycleCts = lifecycleCts;
            LoopPhase = loopPhase;
        }
    }
}
