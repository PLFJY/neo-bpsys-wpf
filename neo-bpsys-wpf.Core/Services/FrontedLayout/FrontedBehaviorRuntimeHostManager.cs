using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 单例管理器，按窗口 ID 创建和跟踪 <see cref="FrontedBehaviorRuntimeHost" /> 实例。
/// 确保在分离时正确清理。
/// </summary>
public sealed class FrontedBehaviorRuntimeHostManager : IDisposable
{
    private readonly IFrontedBehaviorService _behaviorService;
    private readonly IFrontedEventBus _eventBus;
    private readonly IFrontedNodeGraphRuntime _graphRuntime;
    private readonly IFrontedAnimationRuntime _animationRuntime;
    private readonly IFrontedBehaviorAnimationPartRenderer _animationPartRenderer;
    private readonly FrontedBehaviorTriggerEvaluator _triggerEvaluator;
    private readonly ILogger<FrontedBehaviorRuntimeHostManager> _logger;
    private readonly Dictionary<string, FrontedBehaviorRuntimeHost> _hosts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _pendingAttaches = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _windowGates = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="FrontedBehaviorRuntimeHostManager" /> 的新实例。
    /// </summary>
    public FrontedBehaviorRuntimeHostManager(
        IFrontedBehaviorService behaviorService,
        IFrontedEventBus eventBus,
        IFrontedNodeGraphRuntime graphRuntime,
        IFrontedAnimationRuntime animationRuntime,
        IFrontedBehaviorAnimationPartRenderer animationPartRenderer,
        FrontedBehaviorTriggerEvaluator triggerEvaluator,
        ILogger<FrontedBehaviorRuntimeHostManager> logger)
    {
        _behaviorService = behaviorService;
        _eventBus = eventBus;
        _graphRuntime = graphRuntime;
        _animationRuntime = animationRuntime;
        _animationPartRenderer = animationPartRenderer;
        _triggerEvaluator = triggerEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// 为给定上下文附加行为运行时宿主。
    /// 如果同一窗口 ID 的宿主已存在，则先将其分离。
    /// </summary>
    /// <param name="context">窗口行为上下文。</param>
    /// <param name="cancellationToken">取消文档加载和附加的令牌。</param>
    /// <returns>附加完成或被后续请求取代后结束的任务。</returns>
    /// <exception cref="OperationCanceledException">取消令牌已取消。</exception>
    public async Task AttachHostAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_disposed) return;
        }
        var key = BuildKey(context.WindowId);
        var windowGate = _windowGates.GetOrAdd(key, _ => new object());
        var attempt = new object();
        lock (windowGate)
        {
            lock (_gate)
            {
                if (_disposed) return;
            }
            DetachHost(context.WindowId);
            lock (_gate)
            {
                if (_disposed) return;
                _pendingAttaches[key] = attempt;
            }
        }

        try
        {
            var document = await _behaviorService.LoadDocumentAsync(context.WindowType, cancellationToken).WaitAsync(cancellationToken);
            lock (windowGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (_disposed || !_pendingAttaches.TryGetValue(key, out var current) || !ReferenceEquals(current, attempt))
                        return;
                }

                var host = new FrontedBehaviorRuntimeHost(
                    context, _eventBus, _graphRuntime, _animationRuntime, _triggerEvaluator);
                try
                {
                    _animationPartRenderer.ApplyAnimationParts(context.RootCanvas, document);
                    // Attach only subscribes synchronously; no asynchronous work is blocked here.
                    host.AttachAsync(document).GetAwaiter().GetResult();
                    cancellationToken.ThrowIfCancellationRequested();
                    var accepted = false;
                    lock (_gate)
                    {
                        if (!_disposed && _pendingAttaches.TryGetValue(key, out var current) && ReferenceEquals(current, attempt))
                        {
                            _hosts[key] = host;
                            accepted = true;
                        }
                    }
                    if (!accepted)
                    {
                        host.Dispose();
                        return;
                    }
                }
                catch
                {
                    host.Dispose();
                    throw;
                }

                _logger.LogInformation("Behavior host attached: {Key} with {Count} behavior sets.",
                    key, document.ControlBehaviorSets?.Count ?? 0);
                _eventBus.Publish(new FrontedBehaviorEvent
                {
                    EventType = "CanvasLoaded",
                    WindowId = context.WindowId,
                    WindowType = context.WindowType,
                    CanvasName = FrontedLayoutConstants.BaseCanvasName,
                    Source = "WindowLifecycle",
                    Timestamp = DateTimeOffset.UtcNow,
                    IsPreview = context.IsDesignerPreview
                });
            }
        }
        catch
        {
            lock (windowGate)
            {
                FrontedBehaviorRuntimeHost? failedHost = null;
                lock (_gate)
                {
                    if (_pendingAttaches.TryGetValue(key, out var current) && ReferenceEquals(current, attempt))
                        _hosts.Remove(key, out failedHost);
                }
                failedHost?.Dispose();
            }
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (_pendingAttaches.TryGetValue(key, out var current) && ReferenceEquals(current, attempt))
                    _pendingAttaches.Remove(key);
            }
        }
    }

    /// <summary>
    /// 分离并释放给定窗口 ID 的宿主。
    /// 取消所有正在运行的行为，释放事件订阅和动画会话。
    /// </summary>
    /// <param name="windowId">窗口标识符。</param>
    public void DetachHost(string windowId)
    {
        var key = BuildKey(windowId);
        FrontedBehaviorRuntimeHost? existing;
        lock (_gate) { existing = _hosts.GetValueOrDefault(key); }
        if (existing is not null && !existing.Context.RootCanvas.Dispatcher.CheckAccess())
        {
            // Release mutates WPF animation state. Dispatch before taking the per-window lock,
            // so the UI thread can never wait for a lock held by a thread waiting for that UI.
            existing.Context.RootCanvas.Dispatcher.Invoke(() => DetachHost(windowId));
            return;
        }
        if (!_windowGates.TryGetValue(key, out var windowGate)) return;
        lock (windowGate)
        {
            FrontedBehaviorRuntimeHost? host;
            lock (_gate)
            {
                _pendingAttaches.Remove(key);
                _hosts.Remove(key, out host);
            }
            host?.Dispose();
        }
        _logger.LogInformation("Behavior host detached: {Key}", key);
    }

    /// <summary>
    /// 向事件总线发布 ManualTrigger 事件。
    /// </summary>
    public void PublishManualTrigger(string triggerName, string? windowId = null)
    {
        var manualEvent = new FrontedBehaviorEvent
        {
            EventType = "ManualTrigger",
            WindowId = windowId,
            CanvasName = FrontedLayoutConstants.BaseCanvasName,
            Source = "Manual",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new Dictionary<string, object?>
            {
                ["Name"] = triggerName
            }
        };

        _eventBus.Publish(manualEvent);
        _logger.LogInformation("ManualTrigger published: {TriggerName}", triggerName);
    }

    /// <summary>
    /// 停止所有已附加宿主中的活动循环行为。
    /// </summary>
    /// <param name="reason">停止活动循环的原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>请求停止的循环数量。</returns>
    public async Task<int> StopAllLoopBehaviorsAsync(
        FrontedBehaviorStopReason reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<FrontedBehaviorRuntimeHost> hosts;
        lock (_gate)
        {
            hosts = [.. _hosts.Values];
        }

        _logger.LogInformation("Stop all loops requested. Reason={Reason}, HostCount={HostCount}", reason, hosts.Count);
        var count = 0;
        foreach (var host in hosts)
        {
            count += await host.StopAllLoopBehaviorsAsync(reason, TimeSpan.FromMilliseconds(1500), cancellationToken);
        }

        _logger.LogInformation("Stop all loops completed. Reason={Reason}, Count={Count}", reason, count);
        return count;
    }

    /// <summary>
    /// 停止单个已附加前台窗口宿主的活动循环行为。
    /// </summary>
    /// <param name="windowId">前台窗口标识符。</param>
    /// <param name="reason">停止活动循环的原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>请求停止的循环数量。</returns>
    public async Task<int> StopLoopBehaviorsAsync(
        string windowId,
        FrontedBehaviorStopReason reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FrontedBehaviorRuntimeHost? host;
        lock (_gate)
        {
            host = _hosts.GetValueOrDefault(BuildKey(windowId));
        }

        return host is null
            ? 0
            : await host.StopAllLoopBehaviorsAsync(reason, TimeSpan.FromMilliseconds(1500), cancellationToken);
    }

    /// <summary>
    /// 为转场请求创建转场执行匹配。
    /// </summary>
    /// <param name="request">要与已附加宿主匹配的转场请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的转场执行。</returns>
    internal IReadOnlyList<FrontedTransitionExecution> CreateTransitionExecutions(
        FrontedTransitionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<FrontedBehaviorRuntimeHost> hosts;
        lock (_gate)
        {
            hosts = [.. _hosts.Values];
        }

        return hosts
            .Where(host => string.IsNullOrWhiteSpace(request.WindowType) ||
                           string.Equals(host.Context.WindowType, request.WindowType, StringComparison.Ordinal))
            .SelectMany(host => host.CreateTransitionExecutions(request, cancellationToken))
            .ToArray();
    }

    /// <summary>
    /// 分离所有宿主并释放所有资源。
    /// </summary>
    public void Dispose()
    {
        List<FrontedBehaviorRuntimeHost> hosts;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pendingAttaches.Clear();
            hosts = [.. _hosts.Values];
            _hosts.Clear();
        }

        foreach (var host in hosts)
        {
            try
            {
                host.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing behavior host.");
            }
        }

        _windowGates.Clear();

        _logger.LogInformation("All behavior hosts detached ({Count}).", hosts.Count);
    }

    /// <summary>
    /// 获取给定窗口 ID 的宿主，未附加时返回 null。
    /// </summary>
    internal FrontedBehaviorRuntimeHost? GetHost(string windowId)
    {
        var key = BuildKey(windowId);
        lock (_gate)
        {
            return _hosts.GetValueOrDefault(key);
        }
    }

    private static string BuildKey(string windowId) => windowId;
}
