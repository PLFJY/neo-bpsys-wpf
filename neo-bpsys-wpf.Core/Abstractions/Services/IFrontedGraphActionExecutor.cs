using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台行为图动作执行器接口，负责执行单个动画动作请求。
/// </summary>
public interface IFrontedGraphActionExecutor
{
    /// <summary>
    /// 执行指定的动画动作请求。
    /// </summary>
    /// <param name="request">动画动作请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken);
}
