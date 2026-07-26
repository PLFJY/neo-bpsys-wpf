using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 将行为拥有的生成动画部分渲染到已渲染的前台控件中。
/// </summary>
public interface IFrontedBehaviorAnimationPartRenderer
{
    /// <summary>
    /// 将行为文档中的动画部分应用到已渲染的可视化树。
    /// </summary>
    /// <param name="root">已渲染的前台根元素。</param>
    /// <param name="behaviorDocument">包含各控件动画部分的行为文档。</param>
    void ApplyAnimationParts(FrameworkElement root, FrontedBehaviorDocument behaviorDocument);
}
