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
    /// Requests the current gate holder to yield so a modal child window can run its own tutorial.
    /// If the gate is free or the holder is not an ancestor of <paramref name="child"/>, this is a no-op.
    /// </summary>
    /// <param name="child">The modal child window requesting the handoff.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if a handoff was requested; otherwise <see langword="false"/>.</returns>
    Task<bool> RequestChildHandoffAsync(Window child, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies the coordinator that a modal child session has completed and the parent may resume.
    /// </summary>
    void NotifyChildSessionCompleted();
}

/// <summary>Default global tutorial playback coordinator.</summary>
public sealed class TutorialPlaybackCoordinator : ITutorialPlaybackCoordinator
{
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private readonly Dictionary<SequenceRequestKey, Task<TutorialRunResult>> _sequenceJobs = new();
    private readonly object _syncRoot = new();
    private readonly ILogger<TutorialPlaybackCoordinator> _logger;
    private readonly ITutorialStepCancellation? _stepCancellation;
    private FrameworkElement? _currentOwner;
    private TaskCompletionSource? _childHandoffTcs;
    private int _childHandoffRequested;

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
    public Task<bool> RequestChildHandoffAsync(Window child, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(child);

        FrameworkElement? holder;
        lock (_syncRoot)
        {
            holder = _currentOwner;
        }

        if (holder == null || !IsGateHolderAncestorOfChild(holder, child))
        {
            return Task.FromResult(false);
        }

        LogLifecycle("ChildHandoffRequested", child, child.GetType().Name);
        Interlocked.Exchange(ref _childHandoffRequested, 1);
        _childHandoffTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stepCancellation?.CancelCurrentStep();
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public void NotifyChildSessionCompleted()
    {
        _childHandoffTcs?.TrySetResult();
        _logger.LogInformation("Tutorial playback ChildHandoffCompleted.");
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
                TutorialRunResult result;
                try
                {
                    result = await RunJobAsync(key.Owner, key.TutorialKey, playbackAsync, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return TutorialRunResult.Canceled;
                }

                if (ConsumeChildHandoffFlag(result))
                {
                    LogLifecycle("Blocked behind child", key.Owner, key.TutorialKey);
                    var tcs = _childHandoffTcs;
                    if (tcs != null)
                    {
                        try
                        {
                            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());
                            await tcs.Task;
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            return TutorialRunResult.Canceled;
                        }
                    }

                    continue;
                }

                return result;
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

    private bool ConsumeChildHandoffFlag(TutorialRunResult result)
    {
        return Interlocked.CompareExchange(ref _childHandoffRequested, 0, 1) == 1
            && result == TutorialRunResult.Canceled;
    }

    private async Task<TutorialRunResult> RunJobAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            await _playbackGate.WaitAsync(cancellationToken);
            acquired = true;
            lock (_syncRoot)
            {
                _currentOwner = owner;
            }
            LogLifecycle("Started", owner, tutorialKey);
            return await playbackAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogLifecycle(acquired ? "Canceled" : "Canceled before start", owner, tutorialKey);
            return TutorialRunResult.Canceled;
        }
        finally
        {
            lock (_syncRoot)
            {
                _currentOwner = null;
            }
            if (acquired)
            {
                _playbackGate.Release();
            }
        }
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
