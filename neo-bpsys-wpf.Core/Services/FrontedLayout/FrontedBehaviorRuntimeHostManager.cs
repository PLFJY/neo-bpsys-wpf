using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Singleton manager that creates and tracks <see cref="FrontedBehaviorRuntimeHost" /> instances
/// keyed by (windowId, canvasName). Ensures proper cleanup on detach.
/// </summary>
public sealed class FrontedBehaviorRuntimeHostManager : IDisposable
{
    private readonly IFrontedBehaviorService _behaviorService;
    private readonly IFrontedEventBus _eventBus;
    private readonly IFrontedNodeGraphRuntime _graphRuntime;
    private readonly IFrontedAnimationRuntime _animationRuntime;
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
        FrontedBehaviorTriggerEvaluator triggerEvaluator,
        ILogger<FrontedBehaviorRuntimeHostManager> logger)
    {
        _behaviorService = behaviorService;
        _eventBus = eventBus;
        _graphRuntime = graphRuntime;
        _animationRuntime = animationRuntime;
        _triggerEvaluator = triggerEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Attaches a behavior runtime host for the given context.
    /// If a host already exists for the same (windowId, canvasName), it is detached first.
    /// </summary>
    public async Task AttachHostAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger.LogWarning("HostManager is disposed; ignoring AttachHost.");
            return;
        }

        var key = BuildKey(context.WindowId, context.CanvasName);

        // Detach existing host first to prevent duplicate subscriptions
        DetachHost(context.WindowId, context.CanvasName);

        // Load the behavior document
        var document = await _behaviorService.LoadDocumentAsync(
            context.WindowType,
            context.CanvasName,
            cancellationToken);

        if (document.ControlBehaviorSets is null || document.ControlBehaviorSets.Count == 0)
        {
            _logger.LogDebug(
                "No behaviors found for Window={WindowType}, Canvas={CanvasName}. Host will still be created.",
                context.WindowType, context.CanvasName);
        }

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
            CanvasName = context.CanvasName,
            Source = "WindowLifecycle",
            Timestamp = DateTimeOffset.UtcNow,
            IsPreview = context.IsDesignerPreview
        };
        _eventBus.Publish(canvasLoadedEvent);
    }

    /// <summary>
    /// Detaches and disposes the host for the given (windowId, canvasName).
    /// Cancels all running behaviors, releases the event subscription and animation session.
    /// </summary>
    public void DetachHost(string windowId, string canvasName)
    {
        var key = BuildKey(windowId, canvasName);
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
    public void PublishManualTrigger(string triggerName, string? windowId = null, string? canvasName = null)
    {
        var manualEvent = new FrontedBehaviorEvent
        {
            EventType = "ManualTrigger",
            WindowId = windowId,
            CanvasName = canvasName,
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
    /// Gets the host for the given (windowId, canvasName), or null if not attached.
    /// </summary>
    internal FrontedBehaviorRuntimeHost? GetHost(string windowId, string canvasName)
    {
        var key = BuildKey(windowId, canvasName);
        lock (_gate)
        {
            return _hosts.GetValueOrDefault(key);
        }
    }

    private static string BuildKey(string windowId, string canvasName) =>
        $"{windowId}::{canvasName}";
}
