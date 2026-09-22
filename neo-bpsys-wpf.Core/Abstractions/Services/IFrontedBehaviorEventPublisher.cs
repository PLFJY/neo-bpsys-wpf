namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 向统一前台事件总线发布当前插件已注册的语义事件。
/// </summary>
/// <typeparam name="TPlugin">用于绑定插件注册身份的插件入口类型。</typeparam>
public interface IFrontedBehaviorEventPublisher<TPlugin>
{
    /// <summary>
    /// 发布一个当前插件作用域内的已注册事件。
    /// </summary>
    /// <param name="eventId">注册时使用的局部事件标识。</param>
    /// <param name="payload">不带 <c>Event.</c> 前缀的事件负载。</param>
    /// <param name="windowId">可选的目标窗口实例标识。</param>
    /// <param name="windowType">可选的目标窗口类型。</param>
    /// <exception cref="InvalidOperationException">事件未注册或负载不符合注册 schema 时抛出。</exception>
    void Publish(
        string eventId,
        IReadOnlyDictionary<string, object?>? payload = null,
        string? windowId = null,
        string? windowType = null);
}
