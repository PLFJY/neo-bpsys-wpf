using System.Collections.Concurrent;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Publishes and awaits tutorial signals.
/// </summary>
public interface ITutorialSignalService
{
    /// <summary>Publishes a tutorial signal.</summary>
    /// <param name="signalId">Signal id.</param>
    /// <param name="payload">Optional payload.</param>
    void Publish(string signalId, object? payload = null);

    /// <summary>Waits for a tutorial signal.</summary>
    /// <param name="signalId">Signal id.</param>
    /// <param name="predicate">Optional payload predicate.</param>
    /// <param name="timeout">Wait timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The received payload, or null when no payload was published.</returns>
    Task<object?> WaitAsync(
        string signalId,
        Func<object?, bool>? predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default in-memory tutorial signal service.
/// </summary>
public sealed class TutorialSignalService : ITutorialSignalService
{
    private sealed class SignalWaiter
    {
        public Func<object?, bool>? Predicate { get; init; }

        public TaskCompletionSource<object?> Source { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly ConcurrentDictionary<string, List<SignalWaiter>> _waiters = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Publish(string signalId, object? payload = null)
    {
        if (!_waiters.TryGetValue(signalId, out var waiters))
        {
            return;
        }

        List<SignalWaiter> matched;
        lock (waiters)
        {
            matched = waiters
                .Where(waiter => waiter.Predicate?.Invoke(payload) ?? true)
                .ToList();
            foreach (var waiter in matched)
            {
                waiters.Remove(waiter);
            }
        }

        foreach (var waiter in matched)
        {
            waiter.Source.TrySetResult(payload);
        }
    }

    /// <inheritdoc />
    public async Task<object?> WaitAsync(
        string signalId,
        Func<object?, bool>? predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var waiter = new SignalWaiter { Predicate = predicate };
        var waiters = _waiters.GetOrAdd(signalId, _ => []);
        lock (waiters)
        {
            waiters.Add(waiter);
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        await using var registration = linked.Token.Register(() =>
        {
            lock (waiters)
            {
                waiters.Remove(waiter);
            }

            waiter.Source.TrySetCanceled(linked.Token);
        });

        return await waiter.Source.Task.ConfigureAwait(false);
    }
}
