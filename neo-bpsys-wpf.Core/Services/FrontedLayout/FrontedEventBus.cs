using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Collections.Concurrent;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Thread-safe semantic event bus for fronted behavior events.
/// Supports both typed and wildcard (null) subscriptions.
/// Handler exceptions are caught and logged; they never crash the publisher.
/// </summary>
public sealed class FrontedEventBus : IFrontedEventBus, IDisposable
{
    private readonly ILogger<FrontedEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Subscription>> _subscriptions = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedEventBus" />.
    /// </summary>
    public FrontedEventBus(ILogger<FrontedEventBus>? logger = null)
    {
        _logger = logger ?? NullLogger<FrontedEventBus>.Instance;
    }

    /// <inheritdoc />
    public event EventHandler<FrontedBehaviorEvent>? EventPublished;

    /// <inheritdoc />
    public void Publish(FrontedBehaviorEvent behaviorEvent)
    {
        if (_disposed)
        {
            _logger.LogWarning("EventBus is disposed; ignoring publish of {EventType}.", behaviorEvent.EventType);
            return;
        }

        OnEventPublished(behaviorEvent);

        // Collect matching handlers under lock.
        List<Subscription> handlers;
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(behaviorEvent.EventType, out var typed))
            {
                handlers = typed.Where(s => !s.IsDisposed).ToList();
            }
            else
            {
                handlers = [];
            }

            if (_subscriptions.TryGetValue(Subscription.WildcardKey, out var wildcard))
            {
                handlers.AddRange(wildcard.Where(s => !s.IsDisposed));
            }
        }

        // Execute handlers outside lock; catch exceptions individually.
        foreach (var subscription in handlers)
        {
            try
            {
                if (subscription.IsDisposed)
                {
                    continue;
                }

                var task = subscription.Handler(behaviorEvent);
                ObserveHandlerTask(task, behaviorEvent.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "EventBus handler threw an exception for event {EventType}. Continuing with remaining handlers.",
                    behaviorEvent.EventType);
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string? eventType, Func<FrontedBehaviorEvent, Task> handler)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FrontedEventBus));
        }

        var key = eventType ?? Subscription.WildcardKey;
        var subscription = new Subscription(this, key, handler);

        lock (_gate)
        {
            var list = _subscriptions.GetOrAdd(key, _ => []);
            list.Add(subscription);
        }

        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscription.EventType, out var list))
            {
                return;
            }

            list.Remove(subscription);
            if (list.Count == 0)
            {
                _subscriptions.TryRemove(subscription.EventType, out _);
            }
        }
    }

    private void ObserveHandlerTask(Task task, string eventType)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                _logger.LogWarning(task.Exception,
                    "EventBus async handler threw an exception for event {EventType}.",
                    eventType);
            }

            return;
        }

        _ = task.ContinueWith(
            completedTask =>
            {
                if (completedTask.IsFaulted)
                {
                    _logger.LogWarning(completedTask.Exception,
                        "EventBus async handler threw an exception for event {EventType}.",
                        eventType);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OnEventPublished(FrontedBehaviorEvent behaviorEvent)
    {
        try
        {
            EventPublished?.Invoke(this, behaviorEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EventBus.EventPublished handler threw for event {EventType}.",
                behaviorEvent.EventType);
        }
    }

    /// <summary>
    /// Disposes all subscriptions. No further events will be delivered.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            _subscriptions.Clear();
        }
    }

    private sealed class Subscription(
        FrontedEventBus owner,
        string eventType,
        Func<FrontedBehaviorEvent, Task> handler) : IDisposable
    {
        public static readonly string WildcardKey = "__wildcard__";

        public string EventType { get; } = eventType;
        public Func<FrontedBehaviorEvent, Task> Handler { get; } = handler;
        public bool IsDisposed => _disposed;

        private bool _disposed;

        /// <summary>
        /// Removes this subscription from the event bus.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner.Remove(this);
        }
    }
}
