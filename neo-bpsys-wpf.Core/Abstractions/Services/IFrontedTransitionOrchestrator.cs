using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 围绕业务状态变更运行前台过渡行为图。
/// </summary>
public interface IFrontedTransitionOrchestrator
{
    /// <summary>
    /// 运行匹配的过渡退出图，提交业务状态变更，然后运行匹配的进入图。
    /// </summary>
    /// <param name="request">针对单个目标控件的过渡请求。</param>
    /// <param name="commitAsync">在退出图和进入图之间执行的业务状态更新。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>在过渡序列完成后完成的任务。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="request"/> 或 <paramref name="commitAsync"/> 为 null 时抛出。</exception>
    Task RunTransitionAsync(
        FrontedTransitionRequest request,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为所有目标运行匹配的退出图，统一提交一次，然后为所有目标运行匹配的进入图。
    /// </summary>
    /// <param name="requests">针对所有目标控件的过渡请求。</param>
    /// <param name="commitAsync">在退出图和进入图之间执行的业务状态更新。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>在多目标过渡序列完成后完成的任务。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="requests"/> 或 <paramref name="commitAsync"/> 为 null 时抛出。</exception>
    Task RunMultiTargetTransitionAsync(
        IReadOnlyList<FrontedTransitionRequest> requests,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default);
}
