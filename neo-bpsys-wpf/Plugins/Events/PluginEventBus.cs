using System.Collections.Concurrent;
using neo_bpsys_wpf.Core.Plugins.Events;

namespace neo_bpsys_wpf.Plugins.Events;

/// <summary>
/// 插件事件总线实现
/// </summary>
public class PluginEventBus : IPluginEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent pluginEvent, CancellationToken cancellationToken = default)
        where TEvent : IPluginEvent
    {
        var eventType = typeof(TEvent);
        
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        List<Delegate> handlersCopy;
        lock (_lock)
        {
            handlersCopy = [.. handlers];
        }

        foreach (var handler in handlersCopy)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                switch (handler)
                {
                    case Action<TEvent> syncHandler:
                        syncHandler(pluginEvent);
                        break;
                    case Func<TEvent, CancellationToken, Task> asyncHandler:
                        await asyncHandler(pluginEvent, cancellationToken);
                        break;
                }

                // 如果是可取消事件且已被取消，停止传播
                if (pluginEvent is ICancellablePluginEvent { IsCancelled: true })
                    break;
            }
            catch (Exception)
            {
                // 记录异常但继续传播事件
            }
        }
    }

    /// <inheritdoc/>
    public void Publish<TEvent>(TEvent pluginEvent) where TEvent : IPluginEvent
    {
        var eventType = typeof(TEvent);
        
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        List<Delegate> handlersCopy;
        lock (_lock)
        {
            handlersCopy = [.. handlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                if (handler is Action<TEvent> syncHandler)
                {
                    syncHandler(pluginEvent);
                }
                else if (handler is Func<TEvent, CancellationToken, Task> asyncHandler)
                {
                    // 对于异步处理器，同步等待
                    asyncHandler(pluginEvent, CancellationToken.None).GetAwaiter().GetResult();
                }

                // 如果是可取消事件且已被取消，停止传播
                if (pluginEvent is ICancellablePluginEvent { IsCancelled: true })
                    break;
            }
            catch (Exception)
            {
                // 记录异常但继续传播事件
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IPluginEvent
    {
        var eventType = typeof(TEvent);
        
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = [];
                _handlers[eventType] = handlers;
            }
            handlers.Add(handler);
        }

        return new SubscriptionToken(() => Unsubscribe(eventType, handler));
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IPluginEvent
    {
        var eventType = typeof(TEvent);
        
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = [];
                _handlers[eventType] = handlers;
            }
            handlers.Add(handler);
        }

        return new SubscriptionToken(() => Unsubscribe(eventType, handler));
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }

    /// <summary>
    /// 订阅令牌
    /// </summary>
    private sealed class SubscriptionToken(Action unsubscribe) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            unsubscribe();
            _disposed = true;
        }
    }
}
