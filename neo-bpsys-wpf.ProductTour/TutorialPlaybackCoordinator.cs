using System.Runtime.CompilerServices;
using System.Windows;
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
}

/// <summary>Default global tutorial playback coordinator.</summary>
public sealed class TutorialPlaybackCoordinator : ITutorialPlaybackCoordinator
{
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private readonly Dictionary<SequenceRequestKey, Task<TutorialRunResult>> _sequenceJobs = new();
    private readonly object _syncRoot = new();
    private readonly ILogger<TutorialPlaybackCoordinator> _logger;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="logger">Logger.</param>
    public TutorialPlaybackCoordinator(ILogger<TutorialPlaybackCoordinator> logger)
    {
        _logger = logger;
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

    private async Task<TutorialRunResult> RunSequenceJobAsync(
        SequenceRequestKey key,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            return await RunJobAsync(key.Owner, key.TutorialKey, playbackAsync, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            await _playbackGate.WaitAsync(cancellationToken);
            acquired = true;
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
            if (acquired)
            {
                _playbackGate.Release();
            }
        }
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
