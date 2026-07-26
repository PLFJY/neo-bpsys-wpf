using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 管理前台窗口行为执行的运行时服务。
/// 支持单次（事件触发）和循环（状态机）行为。
/// </summary>
public interface IFrontedBehaviorRuntime
{
    /// <summary>
    /// 通过加载窗口的行为文档、订阅 <see cref="IFrontedEventBus" /> 并发布窗口布局加载事件，
    /// 将运行时附加到窗口。
    /// </summary>
    /// <param name="context">运行时上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AttachAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从窗口分离，取消运行中的行为，释放事件订阅
    /// 并释放 <see cref="IFrontedAnimationRuntime" /> 会话。
    /// </summary>
    /// <param name="windowId">窗口标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DetachAsync(string windowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向事件总线发布手动触发事件，用于测试或设计器预览。
    /// </summary>
    /// <param name="triggerName">触发器名称。</param>
    /// <param name="windowId">可选的窗口过滤条件。</param>
    void PublishManualTrigger(string triggerName, string? windowId = null);

    /// <summary>
    /// 停止所有已附加前台行为宿主上的活动循环行为。
    /// </summary>
    /// <param name="reason">停止循环的原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>请求停止的活动循环行为数量。</returns>
    Task<int> StopAllLoopBehaviorsAsync(
        FrontedBehaviorStopReason reason = FrontedBehaviorStopReason.ManualClear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止单个已附加前台窗口上的活动循环行为。
    /// </summary>
    /// <param name="windowId">前台窗口标识。</param>
    /// <param name="reason">停止循环的原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>请求停止的活动循环行为数量。</returns>
    Task<int> StopLoopBehaviorsAsync(
        string windowId,
        FrontedBehaviorStopReason reason,
        CancellationToken cancellationToken = default);
}
