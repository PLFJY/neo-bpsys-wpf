using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Reflection;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Bridges attributed events from shared-data and gameplay services
/// to <see cref="IFrontedEventBus" />.
/// Subscribes to each event on the service interfaces that is annotated with
/// <see cref="FrontedBehaviorEventAttribute" /> and publishes <see cref="FrontedBehaviorEvent" />
/// instances with payload resolved from attributes.
/// </summary>
public sealed class FrontedSharedDataBehaviorEventBridge : IDisposable
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IGameGuidanceService? _gameGuidanceService;
    private readonly ICharacterSelectionService? _characterSelectionService;
    private readonly IFrontedEventBus _eventBus;
    private readonly ILogger<FrontedSharedDataBehaviorEventBridge> _logger;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedSharedDataBehaviorEventBridge" />.
    /// </summary>
    public FrontedSharedDataBehaviorEventBridge(
        ISharedDataService sharedDataService,
        IFrontedEventBus eventBus,
        ILogger<FrontedSharedDataBehaviorEventBridge>? logger = null,
        IGameGuidanceService? gameGuidanceService = null,
        ICharacterSelectionService? characterSelectionService = null)
    {
        _sharedDataService = sharedDataService;
        _gameGuidanceService = gameGuidanceService;
        _characterSelectionService = characterSelectionService;
        _eventBus = eventBus;
        _logger = logger ?? NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance;
    }

    /// <summary>
    /// Starts the bridge by reflecting and subscribing to all attributed events on
    /// ISharedDataService and IGameGuidanceService.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FrontedSharedDataBehaviorEventBridge));
            }

            if (_subscriptions.Count > 0)
            {
                _logger.LogDebug("SharedData bridge already started; skipping.");
                return;
            }

            var scanTargets = new List<(Type InterfaceType, object ServiceInstance, string SourceName)>
            {
                (typeof(ISharedDataService), _sharedDataService, "SharedDataService")
            };

            if (_gameGuidanceService is not null)
            {
                scanTargets.Add((typeof(IGameGuidanceService), _gameGuidanceService, "GameGuidanceService"));
            }

            if (_characterSelectionService is not null)
            {
                scanTargets.Add((typeof(ICharacterSelectionService), _characterSelectionService, "CharacterSelectionService"));
            }

            foreach (var (interfaceType, serviceInstance, sourceName) in scanTargets)
            {
                foreach (var eventInfo in interfaceType.GetEvents(BindingFlags.Instance | BindingFlags.Public))
                {
                    var metadata = eventInfo.GetCustomAttribute<FrontedBehaviorEventAttribute>();
                    if (metadata?.IsEnabled != true)
                    {
                        continue;
                    }

                    var payloadAttributes = eventInfo.GetCustomAttributes<FrontedBehaviorEventPayloadAttribute>().ToArray();
                    SubscribeToEvent(eventInfo, metadata, payloadAttributes, serviceInstance, sourceName);
                }
            }

            _logger.LogInformation("SharedData bridge started: subscribed to {Count} events.", _subscriptions.Count);
        }
    }

    private void SubscribeToEvent(
        EventInfo eventInfo,
        FrontedBehaviorEventAttribute metadata,
        FrontedBehaviorEventPayloadAttribute[] payloadAttributes,
        object serviceInstance,
        string sourceName)
    {
        try
        {
            // Create a handler delegate that matches the event's specific delegate type.
            // The event may be EventHandler (non-generic) or EventHandler<TEventArgs> (generic).
            // We create the correct delegate type via reflection so the add accessor accepts it.
            var handler = CreateMatchingDelegate(eventInfo, metadata, payloadAttributes, serviceInstance, sourceName);
            if (handler is null)
            {
                _logger.LogWarning("Cannot create handler for {EventName}.", eventInfo.Name);
                return;
            }

            // Add the handler via the add accessor on the actual service instance
            var addMethod = eventInfo.GetAddMethod();
            if (addMethod is null)
            {
                _logger.LogWarning("Cannot resolve add method for {EventName}.", eventInfo.Name);
                return;
            }

            addMethod.Invoke(serviceInstance, [handler]);

            // Store the remove method + handler for cleanup
            var removeMethod = eventInfo.GetRemoveMethod();
            if (removeMethod is not null)
            {
                _subscriptions.Add(new EventHandlerDisposable(removeMethod, serviceInstance, handler));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe to shared data event {EventName}.", eventInfo.Name);
        }
    }

    /// <summary>
    /// Creates a delegate of the correct type for the event, wrapping our publish logic.
    /// Supports both <see cref="EventHandler" /> and <see cref="EventHandler{TEventArgs}" />.
    /// </summary>
    private Delegate? CreateMatchingDelegate(
        EventInfo eventInfo,
        FrontedBehaviorEventAttribute metadata,
        FrontedBehaviorEventPayloadAttribute[] payloadAttributes,
        object serviceInstance,
        string sourceName)
    {
        var handlerType = eventInfo.EventHandlerType;

        if (handlerType == typeof(EventHandler))
        {
            EventHandler handler = (sender, args) =>
            {
                var payload = BuildPayload(payloadAttributes, serviceInstance, sender, args);
                PublishBehaviorEvent(metadata, payload, sourceName);
            };
            return handler;
        }

        if (handlerType is not null && handlerType.IsGenericType &&
            handlerType.GetGenericTypeDefinition() == typeof(EventHandler<>))
        {
            // Generic EventHandler<TEventArgs> — create via Delegate.CreateDelegate
            // using a closed generic method so both the delegate type and args type match.
            var eventArgsType = handlerType.GetGenericArguments()[0];
            var openMethod = typeof(FrontedSharedDataBehaviorEventBridge)
                .GetMethod(nameof(CreateGenericHandler), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var closedMethod = openMethod.MakeGenericMethod(eventArgsType);

            var handler = (Delegate)closedMethod.Invoke(this, [metadata, payloadAttributes, serviceInstance, sourceName])!;
            return handler;
        }

        return null;
    }

    /// <summary>
    /// Factory for a closed generic handler delegate.
    /// </summary>
    private Delegate CreateGenericHandler<TEventArgs>(
        FrontedBehaviorEventAttribute metadata,
        FrontedBehaviorEventPayloadAttribute[] payloadAttributes,
        object serviceInstance,
        string sourceName)
    {
        EventHandler<TEventArgs> handler = (sender, args) =>
        {
            var payload = BuildPayload(payloadAttributes, serviceInstance, sender, args);
            PublishBehaviorEvent(metadata, payload, sourceName);
        };
        return handler;
    }

    private void PublishBehaviorEvent(
        FrontedBehaviorEventAttribute metadata,
        IReadOnlyDictionary<string, object?> payload,
        string sourceName)
    {
        var behaviorEvent = new FrontedBehaviorEvent
        {
            EventType = metadata.EventType,
            Source = sourceName,
            Timestamp = DateTimeOffset.UtcNow,
            IsPreview = false,
            Payload = payload
        };

        _eventBus.Publish(behaviorEvent);

        if (string.Equals(metadata.EventType, "Guidance.StepChanged", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Fronted behavior event published: {EventType}; action={Action}; indexes={Indexes}",
                metadata.EventType,
                payload.TryGetValue("Action", out var action) ? action : null,
                payload.TryGetValue("IndexesText", out var indexesText) ? indexesText : FormatIndexes(payload.TryGetValue("Indexes", out var indexes) ? indexes : null));
        }
    }

    private IReadOnlyDictionary<string, object?> BuildPayload(
        FrontedBehaviorEventPayloadAttribute[] payloadAttributes,
        object serviceInstance,
        object? sender,
        object? args)
    {
        if (payloadAttributes.Length == 0)
        {
            return new Dictionary<string, object?>();
        }

        var result = new Dictionary<string, object?>(payloadAttributes.Length);
        foreach (var attr in payloadAttributes)
        {
            var key = attr.Path;
            if (key.StartsWith("Event.", StringComparison.Ordinal))
            {
                key = key["Event.".Length..];
            }

            object? value = null;
            try
            {
                value = attr.Source switch
                {
                    FrontedBehaviorPayloadSource.ServiceProperty => ResolveServiceProperty(serviceInstance, attr.SourcePath),
                    FrontedBehaviorPayloadSource.EventArgsProperty => ResolveEventArgsProperty(args, attr.SourcePath),
                    FrontedBehaviorPayloadSource.SenderProperty => ResolveSenderProperty(sender, attr.SourcePath),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Failed to resolve payload for {Path} on source {Source}.",
                    attr.Path, attr.Source);
            }

            result[key] = value;
        }

        return result;
    }

    private static object? ResolveServiceProperty(object serviceInstance, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        var parts = sourcePath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = serviceInstance;
        foreach (var part in parts)
        {
            if (current is null) return null;
            var property = current.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null) return null;
            current = property.GetValue(current);
        }

        return current;
    }

    private static object? ResolveEventArgsProperty(object? args, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || args is null)
        {
            return null;
        }

        var property = args.GetType().GetProperty(sourcePath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(args);
    }

    private static object? ResolveSenderProperty(object? sender, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || sender is null)
        {
            return null;
        }

        var property = sender.GetType().GetProperty(sourcePath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(sender);
    }

    private static string FormatIndexes(object? indexes)
    {
        if (indexes is IEnumerable<int> values)
        {
            return $"[{string.Join(", ", values)}]";
        }

        return "[]";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        List<IDisposable> subscriptions;
        lock (_gate)
        {
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing SharedData event subscription.");
            }
        }
    }

    private sealed class EventHandlerDisposable : IDisposable
    {
        private readonly MethodInfo _removeMethod;
        private readonly object _target;
        private readonly Delegate _handler;
        private bool _disposed;

        public EventHandlerDisposable(MethodInfo removeMethod, object target, Delegate handler)
        {
            _removeMethod = removeMethod;
            _target = target;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _removeMethod.Invoke(_target, [_handler]);
            }
            catch
            {
                // Best effort unsubscription
            }
        }
    }
}
