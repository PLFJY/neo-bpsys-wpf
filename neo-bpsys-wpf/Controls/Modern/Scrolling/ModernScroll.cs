using System.Windows;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 提供滚动所有权附加属性，用于控制 <see cref="ModernFrame"/> 内容中滚动行为的归属。
/// </summary>
public static class ModernScroll
{
    /// <summary>
    /// <see cref="OwnershipProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty OwnershipProperty =
        DependencyProperty.RegisterAttached(
            "Ownership",
            typeof(ModernScrollOwnership),
            typeof(ModernScroll),
            new FrameworkPropertyMetadata(ModernScrollOwnership.Auto, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// 获取指定元素的 <see cref="OwnershipProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>滚动所有权模式。</returns>
    public static ModernScrollOwnership GetOwnership(DependencyObject obj) =>
        (ModernScrollOwnership)obj.GetValue(OwnershipProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="OwnershipProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">滚动所有权模式。</param>
    public static void SetOwnership(DependencyObject obj, ModernScrollOwnership value) =>
        obj.SetValue(OwnershipProperty, value);
}
