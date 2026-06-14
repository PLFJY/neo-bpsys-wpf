using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台动画运行时服务接口，负责执行动画动作请求并管理动画状态。
/// </summary>
public interface IFrontedAnimationRuntime
{
    /// <summary>
    /// 批量执行一组动画动作请求。
    /// </summary>
    /// <param name="actions">动画动作请求列表。</param>
    /// <param name="context">动画执行上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ExecuteAsync(
        IReadOnlyList<FrontedGraphActionRequest> actions,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行单个动画动作请求。
    /// </summary>
    /// <param name="action">动画动作请求。</param>
    /// <param name="context">动画执行上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ExecuteAsync(
        FrontedGraphActionRequest action,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置指定行为的所有动画目标到初始状态。
    /// </summary>
    /// <param name="behaviorGuid">行为唯一标识。</param>
    /// <param name="context">动画执行上下文。</param>
    void ResetTarget(Guid behaviorGuid, FrontedAnimationExecutionContext context);

    /// <summary>
    /// 重置当前上下文中所有动画目标到初始状态。
    /// </summary>
    /// <param name="context">动画执行上下文。</param>
    void ResetAll(FrontedAnimationExecutionContext context);

    /// <summary>
    /// Releases the runtime session associated with the specified root element.
    /// Cancels any in-flight animations for that session and removes it from internal tracking.
    /// </summary>
    /// <param name="root">The root <see cref="FrameworkElement"/> whose session to release.</param>
    void Release(FrameworkElement root);
}
