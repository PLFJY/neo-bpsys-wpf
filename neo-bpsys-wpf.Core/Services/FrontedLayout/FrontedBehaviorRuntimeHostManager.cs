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
    public async Task AttachHostAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger.LogWarning("HostManager is disposed; ignoring AttachHost.");
            return;
        }

        var key = BuildKey(context.WindowId);

        // Detach existing host first to prevent duplicate subscriptions
        DetachHost(context.WindowId);

        // Load the behavior document
        var document = await _behaviorService.LoadDocumentAsync(
            context.WindowType,
            cancellationToken);

        if (document.ControlBehaviorSets is null || document.ControlBehaviorSets.Count == 0)
        {
            _logger.LogDebug(
                "No behaviors found for Window={WindowType}. Host will still be created.",
                context.WindowType);
        }

        _animationPartRenderer.ApplyAnimationParts(context.RootCanvas, document);

        // Create and attach the host
        var host = new FrontedBehaviorRuntimeHost(
            context,
            _eventBus,
            _graphRuntime,
            _animationRuntime,
            _triggerEvaluator);

        await host.AttachAsync(document);

        lock (_gate)
        {
            _hosts[key] = host;
        }

        _logger.LogInformation(
            "Behavior host attached: {Key} with {Count} behavior sets.",
            key, document.ControlBehaviorSets?.Count ?? 0);

        // Publish CanvasLoaded
        var canvasLoadedEvent = new FrontedBehaviorEvent
        {
            EventType = "CanvasLoaded",
            WindowId = context.WindowId,
            WindowType = context.WindowType,
            CanvasName = FrontedLayoutConstants.BaseCanvasName,
            Source = "WindowLifecycle",
            Timestamp = DateTimeOffset.UtcNow,
            IsPreview = context.IsDesignerPreview
        };
        _eventBus.Publish(canvasLoadedEvent);
    }

    /// <summary>
    /// 分离并释放给定窗口 ID 的宿主。
    /// 取消所有正在运行的行为，释放事件订阅和动画会话。
    /// </summary>
    public void DetachHost(string windowId)
    {
        var key = BuildKey(windowId);
        FrontedBehaviorRuntimeHost? host;

        lock (_gate)
        {
            if (!_hosts.Remove(key, out host))
            {
                return;
            }
        }

        host.Dispose();
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
        if (_disposed) return;
        _disposed = true;

        List<FrontedBehaviorRuntimeHost> hosts;
        lock (_gate)
        {
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
