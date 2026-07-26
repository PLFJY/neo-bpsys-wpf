using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>按顶层窗口序列化教程播放任务并合并所有者序列请求。</summary>
public interface ITutorialPlaybackCoordinator
{
    /// <summary>排队或加入一个所有者序列播放任务。</summary>
    /// <param name="owner">活跃的教程所有者实例。</param>
    /// <param name="tutorialKey">稳定的教程键。</param>
    /// <param name="playbackAsync">在持有全局所有权期间执行的播放体。</param>
    /// <param name="cancellationToken">所有者生命周期的取消令牌。</param>
    /// <returns>共享的播放结果。</returns>
    Task<TutorialRunResult> RunSequenceAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken = default);

    /// <summary>排队一个在所属顶层窗口内序列化的播放任务。</summary>
    /// <param name="owner">教程所有者。</param>
    /// <param name="tutorialKey">用于诊断的教程或流程键。</param>
    /// <param name="playbackAsync">播放体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>播放结果。</returns>
    Task<TutorialRunResult> RunAsync(
        FrameworkElement owner,
        string tutorialKey,
        Func<CancellationToken, Task<TutorialRunResult>> playbackAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始一个子窗口会话并请求当前门持有者让出。
    /// 如果门空闲或持有者不是 <paramref name="child"/> 的祖先，则不执行任何操作。
    /// </summary>
    /// <param name="child">请求交接的子窗口。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当活跃父级已让出时返回作用域会话；否则返回 <see langword="null"/>。</returns>
    Task<ITutorialChildWindowSession?> BeginChildWindowSessionAsync(
        Window child,
        CancellationToken cancellationToken = default);
}

/// <summary>表示一个作用域内的子窗口播放交接。</summary>
public interface ITutorialChildWindowSession : IDisposable
{
    /// <summary>仅完成一次会话并允许父序列恢复。</summary>
    void Complete();
}

/// <summary>默认的按窗口教程播放协调器。</summary>
public sealed class TutorialPlaybackCoordinator : ITutorialPlaybackCoordinator
{
    private static readonly object GlobalPlaybackScope = new();
    private readonly ConditionalWeakTable<object, SemaphoreSlim> _playbackGates = new();
    private readonly Dictionary<SequenceRequestKey, Task<TutorialRunResult>> _sequenceJobs = new();
    private readonly object _syncRoot = new();
    private readonly ILogger<TutorialPlaybackCoordinator> _logger;
    private readonly ITutorialStepCancellation? _stepCancellation;
    private readonly Dictionary<object, PlaybackExecution> _currentExecutions = new();

    /// <summary>初始化协调器。</summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="stepCancellation">用于模态子窗口交接的可选步骤取消服务。</param>
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
        PlaybackExecution? execution;
        lock (_syncRoot)
        {
            execution = _currentExecutions.Values.FirstOrDefault(candidate =>
                candidate.ChildSession == null
                && IsGateHolderAncestorOfChild(candidate.Owner, child));
            if (execution == null)
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
        _stepCancellation?.YieldCurrentStepForChildWindow(execution.Owner);
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
        var scope = ResolvePlaybackScope(owner);
        SemaphoreSlim playbackGate;
        lock (_syncRoot)
        {
            playbackGate = _playbackGates.GetValue(scope, static _ => new SemaphoreSlim(1, 1));
        }

        var acquired = false;
        PlaybackExecution? execution = null;
        try
        {
            await playbackGate.WaitAsync(cancellationToken);
            acquired = true;
            execution = new PlaybackExecution(owner);
            lock (_syncRoot)
            {
                _currentExecutions[scope] = execution;
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
                if (_currentExecutions.TryGetValue(scope, out var currentExecution)
                    && ReferenceEquals(currentExecution, execution))
                {
                    _currentExecutions.Remove(scope);
                }
            }
            if (acquired)
            {
                playbackGate.Release();
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

    private static object ResolvePlaybackScope(FrameworkElement owner) =>
        owner as Window
        ?? Window.GetWindow(owner)
        ?? GlobalPlaybackScope;

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
