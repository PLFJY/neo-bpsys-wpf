using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 用于前台行为事件的语义化事件总线。
/// 线程安全。Publish 不应因处理器异常导致应用崩溃。
/// </summary>
public interface IFrontedEventBus
{
    /// <summary>
    /// 当任意事件发布到总线时触发。
    /// </summary>
    event EventHandler<FrontedBehaviorEvent>? EventPublished;

    /// <summary>
    /// 向所有匹配的订阅者发布 <see cref="FrontedBehaviorEvent" />。
    /// </summary>
    void Publish(FrontedBehaviorEvent behaviorEvent);

    /// <summary>
    /// 订阅指定 <paramref name="eventType" /> 的事件。
    /// 当 <paramref name="eventType" /> 为 null 时，订阅所有事件。
    /// </summary>
    /// <param name="eventType">要过滤的事件类型，为 null 时订阅全部。</param>
    /// <param name="handler">在匹配事件发布时调用的异步处理器。</param>
    /// <returns>一个 <see cref="IDisposable" />，释放时取消订阅。</returns>
    IDisposable Subscribe(string? eventType, Func<FrontedBehaviorEvent, Task> handler);
}
