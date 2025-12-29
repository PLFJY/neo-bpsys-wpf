using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Plugins;

namespace neo_bpsys_wpf.Plugins;

/// <summary>
/// 插件上下文实现
/// Plugin context implementation
/// </summary>
internal class PluginContext : IPluginContext
{
    private readonly Dictionary<Type, List<Delegate>> _eventHandlers = new();
    private readonly object _lock = new();

    public PluginContext(
        IServiceProvider services,
        ILoggerFactory loggerFactory,
        string pluginDirectory,
        string appDataDirectory)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        AppDataDirectory = appDataDirectory ?? throw new ArgumentNullException(nameof(appDataDirectory));
    }

    /// <inheritdoc />
    public IServiceProvider Services { get; }

    /// <inheritdoc />
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc />
    public string PluginDirectory { get; }

    /// <inheritdoc />
    public string AppDataDirectory { get; }

    /// <inheritdoc />
    public void PublishEvent<TEvent>(TEvent eventData) where TEvent : class
    {
        if (eventData == null) throw new ArgumentNullException(nameof(eventData));

        lock (_lock)
        {
            if (_eventHandlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                foreach (var handler in handlers.ToList())
                {
                    try
                    {
                        ((Action<TEvent>)handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        var logger = LoggerFactory.CreateLogger<PluginContext>();
                        logger.LogError(ex, "Error invoking event handler for {EventType}", typeof(TEvent).Name);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public void SubscribeEvent<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            if (!_eventHandlers.ContainsKey(typeof(TEvent)))
            {
                _eventHandlers[typeof(TEvent)] = new List<Delegate>();
            }

            _eventHandlers[typeof(TEvent)].Add(handler);
        }
    }
}
