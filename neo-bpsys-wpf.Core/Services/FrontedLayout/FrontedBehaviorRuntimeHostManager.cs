using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Singleton manager that creates and tracks <see cref="FrontedBehaviorRuntimeHost" /> instances
/// keyed by window id. Ensures proper cleanup on detach.
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
    /// Initializes a new instance of <see cref="FrontedBehaviorRuntimeHostManager" />.
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
    /// Attaches a behavior runtime host for the given context.
    /// If a host already exists for the same window id, it is detached first.
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
    /// Detaches and disposes the host for the given window id.
    /// Cancels all running behaviors, releases the event subscription and animation session.
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
    /// Publishes a ManualTrigger event to the event bus.
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
    /// Stops all active loop behaviors across all attached hosts.
    /// </summary>
    /// <param name="reason">The reason for stopping active loops.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of loops that were requested to stop.</returns>
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
    /// Stops active loop behaviors for one attached fronted window host.
    /// </summary>
    /// <param name="windowId">The fronted window identifier.</param>
    /// <param name="reason">The reason for stopping active loops.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of loops that were requested to stop.</returns>
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
    /// Creates transition execution matches for a transition request.
    /// </summary>
    /// <param name="request">Transition request to match against attached hosts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matched transition executions.</returns>
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
    /// Detaches all hosts and releases all resources.
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
    /// Gets the host for the given window id, or null if not attached.
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
