using System.Collections.Concurrent;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 发布并等待教程信号。
/// </summary>
public interface ITutorialSignalService
{
    /// <summary>发布一个教程信号。</summary>
    /// <param name="signalId">信号 id。</param>
    /// <param name="payload">可选的负载。</param>
    void Publish(string signalId, object? payload = null);

    /// <summary>等待一个教程信号。</summary>
    /// <param name="signalId">信号 id。</param>
    /// <param name="predicate">可选的负载断言。</param>
    /// <param name="timeout">等待超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>接收到的负载；若未发布任何负载则返回 null。</returns>
    Task<object?> WaitAsync(
        string signalId,
        Func<object?, bool>? predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// 默认的内存内教程信号服务。
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
