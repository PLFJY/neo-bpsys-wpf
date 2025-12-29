namespace neo_bpsys_wpf.Core.Plugins.Events;

/// <summary>
/// 插件事件总线接口
/// </summary>
public interface IPluginEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="pluginEvent">事件实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync<TEvent>(TEvent pluginEvent, CancellationToken cancellationToken = default)
        where TEvent : IPluginEvent;

    /// <summary>
    /// 同步发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="pluginEvent">事件实例</param>
    void Publish<TEvent>(TEvent pluginEvent) where TEvent : IPluginEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>订阅令牌，用于取消订阅</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IPluginEvent;

    /// <summary>
    /// 订阅异步事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">异步事件处理器</param>
    /// <returns>订阅令牌，用于取消订阅</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IPluginEvent;
}

/// <summary>
/// 插件事件处理器接口
/// </summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public interface IPluginEventHandler<in TEvent> where TEvent : IPluginEvent
{
    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="pluginEvent">事件实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TEvent pluginEvent, CancellationToken cancellationToken = default);
}
