using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 可动画属性适配器接口，用于将动画操作桥接到具体的控件属性上。
/// </summary>
public interface IAnimatablePropertyAdapter
{
    /// <summary>
    /// 判断当前适配器是否能处理指定目标和属性。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>如果能处理则返回 <c>true</c>。</returns>
    bool CanHandle(FrontedAnimationTarget target, string propertyName);

    /// <summary>
    /// 捕获属性的当前值作为动画基准值。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>属性的基准值。</returns>
    object? CaptureBaseValue(FrontedAnimationTarget target, string propertyName);

    /// <summary>
    /// 直接设置属性值（非动画方式）。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="value">要设置的值。</param>
    /// <param name="context">动画执行上下文。</param>
    void SetValue(
        FrontedAnimationTarget target,
        string propertyName,
        string? value,
        FrontedAnimationExecutionContext context);

    /// <summary>
    /// 以动画方式将属性从起始值过渡到目标值。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="from">动画起始值。</param>
    /// <param name="to">动画目标值。</param>
    /// <param name="durationMs">动画持续时间（毫秒）。</param>
    /// <param name="easing">缓动函数名称。</param>
    /// <param name="context">动画执行上下文。</param>
    Task AnimateAsync(
        FrontedAnimationTarget target,
        string propertyName,
        string? from,
        string? to,
        int durationMs,
        string? easing,
        FrontedAnimationExecutionContext context);

    /// <summary>
    /// 将属性重置回基准值。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="baseValue">基准值。</param>
    /// <param name="context">动画执行上下文。</param>
    void ResetValue(
        FrontedAnimationTarget target,
        string propertyName,
        object? baseValue,
        FrontedAnimationExecutionContext context);
}
