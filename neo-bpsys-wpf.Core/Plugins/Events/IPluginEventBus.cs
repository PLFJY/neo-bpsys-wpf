namespace neo_bpsys_wpf.Core.Plugins.Events;

/// <summary>
/// 插件事件基类
/// </summary>
public abstract class PluginEvent
{
    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// 事件源插件ID（可选）
    /// </summary>
    public string? SourcePluginId { get; set; }
}

/// <summary>
/// 插件事件处理器委托
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
/// <param name="event">事件实例</param>
public delegate Task PluginEventHandler<in TEvent>(TEvent @event) where TEvent : PluginEvent;

/// <summary>
/// 插件事件总线接口，用于插件间通信
/// </summary>
public interface IPluginEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件实例</param>
    /// <returns>异步任务</returns>
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : PluginEvent;

    /// <summary>
    /// 同步发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件实例</param>
    void Publish<TEvent>(TEvent @event) where TEvent : PluginEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>订阅令牌，用于取消订阅</returns>
    IDisposable Subscribe<TEvent>(PluginEventHandler<TEvent> handler) where TEvent : PluginEvent;

    /// <summary>
    /// 订阅事件（带插件ID标识）
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <param name="handler">事件处理器</param>
    /// <returns>订阅令牌，用于取消订阅</returns>
    IDisposable Subscribe<TEvent>(string pluginId, PluginEventHandler<TEvent> handler) where TEvent : PluginEvent;

    /// <summary>
    /// 取消指定插件的所有订阅
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    void UnsubscribeAll(string pluginId);
}

/// <summary>
/// 应用程序启动完成事件
/// </summary>
public sealed class ApplicationStartedEvent : PluginEvent { }

/// <summary>
/// 应用程序关闭事件
/// </summary>
public sealed class ApplicationShuttingDownEvent : PluginEvent { }

/// <summary>
/// 主题变更事件
/// </summary>
public sealed class ThemeChangedEvent : PluginEvent
{
    /// <summary>
    /// 新主题名称
    /// </summary>
    public required string NewTheme { get; init; }

    /// <summary>
    /// 旧主题名称
    /// </summary>
    public string? OldTheme { get; init; }
}

/// <summary>
/// 插件加载完成事件
/// </summary>
public sealed class PluginLoadedEvent : PluginEvent
{
    /// <summary>
    /// 加载的插件元数据
    /// </summary>
    public required PluginMetadata PluginMetadata { get; init; }
}

/// <summary>
/// 插件卸载事件
/// </summary>
public sealed class PluginUnloadedEvent : PluginEvent
{
    /// <summary>
    /// 卸载的插件ID
    /// </summary>
    public required string PluginId { get; init; }
}
