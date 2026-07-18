using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 创建并查询 v3 控件的纯运行时效果宿主。
/// </summary>
public static class FrontedEffectHostFactory
{
    /// <summary>
    /// 为元素创建效果宿主，并仅转移直接父面板需要的附加布局属性。
    /// </summary>
    /// <param name="element">要保持为语义身份的元素。</param>
    /// <returns>现有或新建的效果宿主。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="element"/> 为 <c>null</c> 时抛出。</exception>
    public static FrontedEffectHost Wrap(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is FrontedEffectHost host)
        {
            return host;
        }

        if (VisualTreeHelper.GetParent(element) is FrontedEffectHost existingHost)
        {
            return existingHost;
        }

        var result = new FrontedEffectHost(element);
        TransferAttachedLayout(element, result);
        return result;
    }

    /// <summary>
    /// 获取元素所属的最近效果宿主。
    /// </summary>
    /// <param name="element">要检查的元素。</param>
    /// <returns>最近的效果宿主；找不到时为 <c>null</c>。</returns>
    public static FrontedEffectHost? FindEffectHost(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrontedEffectHost host)
            {
                return host;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取效果宿主或元素本身保持的语义生成元素。
    /// </summary>
    /// <param name="element">效果宿主或语义元素。</param>
    /// <returns>语义生成元素。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="element"/> 为 <c>null</c> 时抛出。</exception>
    public static FrameworkElement ResolveSemanticElement(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element is FrontedEffectHost host ? host.HostedElement : element;
    }

    internal static FrameworkElement ResolveLayoutCarrier(FrameworkElement element)
    {
        var host = FindEffectHost(element);
        if (host is null)
        {
            return element;
        }

        // Behavior overlay grids are the direct Canvas child and therefore own only
        // the panel-attached layout values transferred from the effect host.
        return VisualTreeHelper.GetParent(host) is Grid overlayHost
               && overlayHost.Children.Contains(host)
               && VisualTreeHelper.GetParent(overlayHost) is Panel
            ? overlayHost
            : host;
    }

    internal static void TransferAttachedLayout(FrameworkElement source, FrameworkElement destination)
    {
        TransferLocalValue(source, destination, Canvas.LeftProperty);
        TransferLocalValue(source, destination, Canvas.TopProperty);
        TransferLocalValue(source, destination, Canvas.RightProperty);
        TransferLocalValue(source, destination, Canvas.BottomProperty);
        TransferLocalValue(source, destination, Panel.ZIndexProperty);
    }

    private static void TransferLocalValue(DependencyObject source, DependencyObject destination, DependencyProperty property)
    {
        var value = source.ReadLocalValue(property);
        if (value != DependencyProperty.UnsetValue)
        {
            destination.SetValue(property, value);
        }

        source.ClearValue(property);
    }
}
