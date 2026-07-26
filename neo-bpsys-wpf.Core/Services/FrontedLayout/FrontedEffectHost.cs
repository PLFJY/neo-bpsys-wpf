using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为 v3 生成控件提供纯运行时的单子元素视觉宿主。
/// </summary>
/// <remarks>
/// 宿主不拥有被宿主元素的尺寸、对齐、可见性或视觉属性。显式测量和排列保证宿主
/// 只报告子元素的期望尺寸，并将 Canvas 给它的同一排列槽交给子元素。
/// </remarks>
public sealed class FrontedEffectHost : Decorator
{
    /// <summary>
    /// 使用指定的语义生成控件创建宿主。
    /// </summary>
    /// <param name="hostedElement">要保持为语义身份的生成控件。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="hostedElement"/> 为 <c>null</c> 时抛出。</exception>
    public FrontedEffectHost(FrameworkElement hostedElement)
    {
        ArgumentNullException.ThrowIfNull(hostedElement);
        Child = hostedElement;
    }

    /// <summary>
    /// 获取保持控件语义身份的被宿主元素。
    /// </summary>
    public FrameworkElement HostedElement => (FrameworkElement)Child;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        Child.Measure(constraint);
        return Child.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        // The Canvas gives the host the same slot formerly given to the semantic root.
        // FrameworkElement alignment remains owned and interpreted by that root.
        Child.Arrange(new Rect(arrangeSize));
        return arrangeSize;
    }
}
