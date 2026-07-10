using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>Serializes all tutorial playback jobs and coalesces owner-sequence requests.</summary>
public interface ITutorialPlaybackCoordinator
{
    /// <summary>Queues or joins an owner-sequence playback job.</summary>
    /// <param name="owner">Live tutorial owner instance.</param>
    /// <param name="tutorialKey">Stable tutorial key.</param>
    /// <param name="playbackAsync">Playback body executed while global ownership is retained.</param>
    /// <param name="cancellationToken">Owner lifetime cancellation token.</param>
    /// <returns>The shared playback result.</returns>
    Task<TutorialRunResult> RunSequenceAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken = default);

    /// <summary>Queues a globally serialized playback job.</summary>
    /// <param name="owner">Tutorial owner.</param>
    /// <param name="tutorialKey">Diagnostic tutorial or flow key.</param>
    /// <param name="playbackAsync">Playback body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The playback result.</returns>
    Task<TutorialRunResult> RunAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a child-window session and requests the current gate holder to yield.
    /// If the gate is free or the holder is not an ancestor of <paramref name="child"/>, this is a no-op.
    /// </summary>
    /// <param name="child">The child window requesting the handoff.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A scoped session when the active parent yielded; otherwise <see langword="null"/>.</returns>
    Task<ITutorialChildWindowSession?> BeginChildWindowSessionAsync(
        Window child,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents one scoped child-window playback handoff.</summary>
public interface ITutorialChildWindowSession : IDisposable
{
    /// <summary>Completes the session exactly once and allows the parent sequence to resume.</summary>
    void Complete();
}

/// <summary>Default global tutorial playback coordinator.</summary>
public sealed class TutorialPlaybackCoordinator : ITutorialPlaybackCoordinator
{
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private readonly Dictionary<SequenceRequestKey, Task<TutorialRunResult>> _sequenceJobs = new();
    private readonly object _syncRoot = new();
    private readonly ILogger<TutorialPlaybackCoordinator> _logger;
    private readonly ITutorialStepCancellation? _stepCancellation;
    private PlaybackExecution? _currentExecution;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="stepCancellation">Optional step cancellation service for modal child handoff.</param>
    public TutorialPlaybackCoordinator(
        ILogger<TutorialPlaybackCoordinator> logger,
        ITutorialStepCancellation? stepCancellation = null)
    {
        _logger = logger;
        _stepCancellation = stepCancellation;
    }

    /// <inheritdoc />
    public Task<TutorialRunResult> RunSequenceAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(tutorialKey);
        ArgumentNullException.ThrowIfNull(playbackAsync);

        var key = new SequenceRequestKey(owner, tutorialKey);
        lock (_syncRoot)
        {
            if (_sequenceJobs.TryGetValue(key, out var existing))
            {
                LogLifecycle("Coalesced", owner, tutorialKey);
                return existing;
            }

            LogLifecycle("Queued", owner, tutorialKey);
            var task = RunSequenceJobAsync(key, playbackAsync, cancellationToken);
            _sequenceJobs.Add(key, task);
            return task;
        }
    }

    /// <inheritdoc />
    public Task<TutorialRunResult> RunAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(tutorialKey);
        ArgumentNullException.ThrowIfNull(playbackAsync);
        LogLifecycle("Queued", owner, tutorialKey);
        return RunJobAsync(owner, tutorialKey, playbackAsync, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ITutorialChildWindowSession?> BeginChildWindowSessionAsync(
        Window child,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(child);
        cancellationToken.ThrowIfCancellationRequested();

        ChildWindowSession? session;
        lock (_syncRoot)
        {
            var execution = _currentExecution;
            if (execution == null
                || execution.ChildSession != null
                || !IsGateHolderAncestorOfChild(execution.Owner, child))
            {
                return Task.FromResult<ITutorialChildWindowSession?>(null);
            }

            session = new ChildWindowSession(
                () => _logger.LogInformation(
                    "Tutorial playback ChildWindowSessionCompleted. ChildType={ChildType}",
                    child.GetType().FullName),
                cancellationToken);
            execution.ChildSession = session;
        }

        LogLifecycle("ChildHandoffRequested", child, child.GetType().Name);
        _stepCancellation?.YieldCurrentStepForChildWindow();
        return Task.FromResult<ITutorialChildWindowSession?>(session);
    }

    private async Task<TutorialRunResult> RunSequenceJobAsync(
        SequenceRequestKey key,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            while (true)
            {
                PlaybackOutcome outcome;
                try
                {
                    outcome = await RunJobCoreAsync(key.Owner, key.TutorialKey, playbackAsync, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return TutorialRunResult.Canceled;
                }

                if (outcome is { Result: TutorialRunResult.ChildWindowHandoff, ChildSession: { } childSession })
                {
                    LogLifecycle("Blocked behind child", key.Owner, key.TutorialKey);
                    try
                    {
                        await childSession.Completion.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return TutorialRunResult.Canceled;
                    }

                    continue;
                }

                return outcome.Result;
            }
        }
        finally
        {
            lock (_syncRoot)
            {
                _sequenceJobs.Remove(key);
            }
        }
    }

    private async Task<TutorialRunResult> RunJobAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken) =>
        (await RunJobCoreAsync(owner, tutorialKey, playbackAsync, cancellationToken)).Result;

    private async Task<PlaybackOutcome> RunJobCoreAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken)
    {
        var acquired = false;
        PlaybackExecution? execution = null;
        try
        {
            await _playbackGate.WaitAsync(cancellationToken);
            acquired = true;
            execution = new PlaybackExecution(owner);
            lock (_syncRoot)
            {
                _currentExecution = execution;
            }
            LogLifecycle("Started", owner, tutorialKey);
            var result = await playbackAsync(cancellationToken);
            return new PlaybackOutcome(result, execution.ChildSession);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogLifecycle(acquired ? "Canceled" : "Canceled before start", owner, tutorialKey);
            return new PlaybackOutcome(TutorialRunResult.Canceled, execution?.ChildSession);
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_currentExecution, execution))
                {
                    _currentExecution = null;
                }
            }
            if (acquired)
            {
                _playbackGate.Release();
            }
        }
    }

    private sealed class PlaybackExecution(FrameworkElement owner)
    {
        public FrameworkElement Owner { get; } = owner;

        public ChildWindowSession? ChildSession { get; set; }
    }

    private readonly record struct PlaybackOutcome(
        TutorialRunResult Result,
        ChildWindowSession? ChildSession);

    private sealed class ChildWindowSession : ITutorialChildWindowSession
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action _completed;
        private readonly CancellationTokenRegistration _cancellationRegistration;
        private int _isCompleted;

        public ChildWindowSession(Action completed, CancellationToken cancellationToken)
        {
            _completed = completed;
            _cancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static state => ((ChildWindowSession)state!).Complete(), this)
                : default;
            if (Volatile.Read(ref _isCompleted) != 0)
            {
                _cancellationRegistration.Dispose();
            }
        }

        public Task Completion => _completion.Task;

        public void Complete()
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }

            _cancellationRegistration.Dispose();
            _completion.TrySetResult();
            _completed();
        }

        public void Dispose() => Complete();
    }

    private static bool IsGateHolderAncestorOfChild(FrameworkElement gateHolder, Window child)
    {
        var ownerWindow = child.Owner;
        if (ownerWindow == null)
        {
            return false;
        }

        if (gateHolder is Window gateHolderWindow)
        {
            var current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, gateHolderWindow))
                {
                    return true;
                }
                current = current.Owner;
            }
            return false;
        }

        return IsDescendantOfWindow(gateHolder, ownerWindow);
    }

    private static bool IsDescendantOfWindow(DependencyObject element, DependencyObject ancestor)
    {
        var current = element;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private void LogLifecycle(string lifecycle, FrameworkElement owner, string tutorialKey) =>
        _logger.LogInformation(
            "Tutorial playback {Lifecycle}. TutorialKey={TutorialKey}, OwnerType={OwnerType}, OwnerIdentity={OwnerIdentity}",
            lifecycle,
            tutorialKey,
            owner.GetType().FullName,
            RuntimeHelpers.GetHashCode(owner));

    private sealed class SequenceRequestKey : IEquatable<SequenceRequestKey>
    {
        public SequenceRequestKey(FrameworkElement owner, string tutorialKey)
        {
            Owner = owner;
            TutorialKey = tutorialKey;
        }

        public FrameworkElement Owner { get; }

        public string TutorialKey { get; }

        public bool Equals(SequenceRequestKey? other) => other != null
            && ReferenceEquals(Owner, other.Owner)
            && string.Equals(TutorialKey, other.TutorialKey, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as SequenceRequestKey);

        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Owner), TutorialKey);
    }
}
