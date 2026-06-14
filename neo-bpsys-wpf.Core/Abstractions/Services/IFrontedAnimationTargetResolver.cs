using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台动画目标解析器接口，负责将动画目标引用解析为实际的可操作目标对象。
/// </summary>
public interface IFrontedAnimationTargetResolver
{
    /// <summary>
    /// 根据目标引用解析出实际的动画目标。
    /// </summary>
    /// <param name="reference">动画目标引用。</param>
    /// <param name="context">动画执行上下文。</param>
    /// <returns>解析后的动画目标，如果无法解析则返回 <c>null</c>。</returns>
    FrontedAnimationTarget? Resolve(
        FrontedAnimationTargetReference reference,
        FrontedAnimationExecutionContext context);
}
