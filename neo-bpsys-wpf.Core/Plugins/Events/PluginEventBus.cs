using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.Core.Plugins.Events;

/// <summary>
/// 插件事件总线实现
/// </summary>
public sealed class PluginEventBus : IPluginEventBus
{
    private readonly ILogger<PluginEventBus> _logger;
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly object _lock = new();

    /// <summary>
    /// 创建事件总线
    /// </summary>
    public PluginEventBus(ILogger<PluginEventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : PluginEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var subscriptions))
        {
            return;
        }

        List<Subscription> handlers;
        lock (_lock)
        {
            handlers = subscriptions.ToList();
        }

        foreach (var subscription in handlers)
        {
            try
            {
                if (subscription.Handler is PluginEventHandler<TEvent> handler)
                {
                    await handler(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling event {EventType} by plugin {PluginId}",
                    eventType.Name, subscription.PluginId ?? "Unknown");
            }
        }
    }

    /// <inheritdoc/>
    public void Publish<TEvent>(TEvent @event) where TEvent : PluginEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var subscriptions))
        {
            return;
        }

        List<Subscription> handlers;
        lock (_lock)
        {
            handlers = subscriptions.ToList();
        }

        foreach (var subscription in handlers)
        {
            try
            {
                if (subscription.Handler is PluginEventHandler<TEvent> handler)
                {
                    // 同步执行，忽略返回的Task
                    _ = handler(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling event {EventType} by plugin {PluginId}",
                    eventType.Name, subscription.PluginId ?? "Unknown");
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(PluginEventHandler<TEvent> handler) where TEvent : PluginEvent
    {
        return Subscribe(null, handler);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(string? pluginId, PluginEventHandler<TEvent> handler) where TEvent : PluginEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var subscription = new Subscription(pluginId, handler);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var subscriptions))
            {
                subscriptions = new List<Subscription>();
                _subscriptions[eventType] = subscriptions;
            }

            subscriptions.Add(subscription);
        }

        _logger.LogDebug("Subscribed to event {EventType} by plugin {PluginId}",
            eventType.Name, pluginId ?? "Unknown");

        return new SubscriptionToken(() =>
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(eventType, out var subscriptions))
                {
                    subscriptions.Remove(subscription);
                }
            }

            _logger.LogDebug("Unsubscribed from event {EventType} by plugin {PluginId}",
                eventType.Name, pluginId ?? "Unknown");
        });
    }

    /// <inheritdoc/>
    public void UnsubscribeAll(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        lock (_lock)
        {
            foreach (var subscriptions in _subscriptions.Values)
            {
                subscriptions.RemoveAll(s => s.PluginId == pluginId);
            }
        }

        _logger.LogDebug("Unsubscribed all events for plugin {PluginId}", pluginId);
    }

    /// <summary>
    /// 订阅信息
    /// </summary>
    private sealed class Subscription
    {
        public string? PluginId { get; }
        public Delegate Handler { get; }

        public Subscription(string? pluginId, Delegate handler)
        {
            PluginId = pluginId;
            Handler = handler;
        }
    }

    /// <summary>
    /// 订阅令牌
    /// </summary>
    private sealed class SubscriptionToken : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
