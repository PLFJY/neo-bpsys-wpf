using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 可动画属性适配器注册表接口，用于根据目标类型和属性名查找对应的适配器。
/// </summary>
public interface IAnimatablePropertyAdapterRegistry
{
    /// <summary>
    /// 根据动画目标和属性名解析对应的适配器。
    /// </summary>
    /// <param name="target">动画目标。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <returns>对应的适配器，如果未找到则返回 <c>null</c>。</returns>
    IAnimatablePropertyAdapter? Resolve(FrontedAnimationTarget target, string propertyName);
}
