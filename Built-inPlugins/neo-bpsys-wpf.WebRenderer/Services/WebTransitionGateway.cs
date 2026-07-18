using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Collections.Concurrent;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>向受控 Web 页面发布过渡生命周期并等待其确认。</summary>
public interface IWebTransitionGateway
{
    /// <summary>获取当前 bootstrap generation。</summary>
    long CurrentGeneration { get; }

    /// <summary>更新当前 bootstrap generation 并取消旧会话。</summary>
    /// <param name="generation">新的 generation。</param>
    void UpdateGeneration(long generation);
    /// <summary>准备一次 Web 过渡。</summary>
    /// <param name="requests">同一次业务提交的目标请求。</param>
    /// <param name="generation">当前 bootstrap generation。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>过渡会话。</returns>
    WebTransitionSession Prepare(IReadOnlyList<FrontedTransitionRequest> requests, long generation, CancellationToken cancellationToken);

    /// <summary>等待 Web Exit 完成；调用方负责 fail-open 超时策略。</summary>
    Task WaitForExitAsync(WebTransitionSession session, CancellationToken cancellationToken);

    /// <summary>通知 Web 已完成唯一的业务提交。</summary>
    void Commit(WebTransitionSession session);

    /// <summary>等待 Web Enter 完成。</summary>
    Task WaitForEnterAsync(WebTransitionSession session, CancellationToken cancellationToken);

    /// <summary>取消并释放一次过渡。</summary>
    void Cancel(WebTransitionSession session, string reason);

    /// <summary>处理 sidecar 回传的确认消息。</summary>
    void Acknowledge(string correlationId, bool enter);
}

/// <summary>一次跨端过渡的不可变标识和等待句柄。</summary>
public sealed class WebTransitionSession
{
    internal WebTransitionSession(string correlationId, long generation, IReadOnlyList<FrontedTransitionRequest> requests)
    {
        CorrelationId = correlationId;
        Generation = generation;
        Requests = requests;
    }

    /// <summary>获取关联标识。</summary>
    public string CorrelationId { get; }
    /// <summary>获取布局 generation。</summary>
    public long Generation { get; }
    /// <summary>获取过渡目标。</summary>
    public IReadOnlyList<FrontedTransitionRequest> Requests { get; }
    internal TaskCompletionSource Exit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Enter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>由 sidecar 服务实现的进程内过渡网关。</summary>
public sealed class WebTransitionGateway : IWebTransitionGateway
{
    private readonly ConcurrentDictionary<string, WebTransitionSession> _sessions = new(StringComparer.Ordinal);
    private long _generation;

    /// <inheritdoc />
    public long CurrentGeneration => Interlocked.Read(ref _generation);

    /// <summary>发生需要发送给 sidecar 的消息。</summary>
    public event EventHandler<WebTransitionSignal>? SignalPublished;

    /// <inheritdoc />
    public void UpdateGeneration(long generation)
    {
        Interlocked.Exchange(ref _generation, generation);
        foreach (var session in _sessions.Values.Where(item => item.Generation != generation).ToArray()) Cancel(session, "layout-changed");
    }

    /// <inheritdoc />
    public WebTransitionSession Prepare(IReadOnlyList<FrontedTransitionRequest> requests, long generation, CancellationToken cancellationToken)
    {
        var session = new WebTransitionSession(Guid.NewGuid().ToString("N"), generation, requests);
        _sessions[session.CorrelationId] = session;
        cancellationToken.Register(() => Cancel(session, "cancelled"));
        SignalPublished?.Invoke(this, new WebTransitionSignal(WebRendererIpcProtocol.TransitionPrepare, session, null));
        return session;
    }

    /// <inheritdoc />
    public Task WaitForExitAsync(WebTransitionSession session, CancellationToken cancellationToken) => session.Exit.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Commit(WebTransitionSession session) => SignalPublished?.Invoke(this, new WebTransitionSignal(WebRendererIpcProtocol.TransitionCommitted, session, null));

    /// <inheritdoc />
    public Task WaitForEnterAsync(WebTransitionSession session, CancellationToken cancellationToken) => session.Enter.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Cancel(WebTransitionSession session, string reason)
    {
        if (_sessions.TryRemove(session.CorrelationId, out _))
        {
            session.Exit.TrySetCanceled();
            session.Enter.TrySetCanceled();
            SignalPublished?.Invoke(this, new WebTransitionSignal(WebRendererIpcProtocol.TransitionCancel, session, reason));
        }
    }

    /// <inheritdoc />
    public void Acknowledge(string correlationId, bool enter)
    {
        if (!_sessions.TryGetValue(correlationId, out var session)) return;
        if (enter)
        {
            session.Enter.TrySetResult();
            _sessions.TryRemove(correlationId, out _);
        }
        else session.Exit.TrySetResult();
    }
}

/// <summary>sidecar 可序列化的过渡信号。</summary>
public sealed record WebTransitionSignal(string Type, WebTransitionSession Session, string? Reason);
