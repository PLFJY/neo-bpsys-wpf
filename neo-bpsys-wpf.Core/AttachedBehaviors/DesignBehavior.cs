using neo_bpsys_wpf.Core.Controls;
using System.Windows;
using System.Windows.Documents;

namespace neo_bpsys_wpf.Core.AttachedBehaviors;

/// <summary>
/// 设计模式下的附加属性
/// </summary>
public static class DesignBehavior
{
    /// <summary>
    /// 是否为设计模式
    /// </summary>
    public static readonly DependencyProperty IsDesignerModeProperty =
        DependencyProperty.RegisterAttached("IsDesignerMode", typeof(bool), typeof(DesignBehavior),
            new PropertyMetadata(false, OnIsDesignerModeChanged));

    /// <summary>
    /// 获取是否为设计模式
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static bool GetIsDesignerMode(UIElement element)
    {
        return (bool)element.GetValue(IsDesignerModeProperty);
    }

    /// <summary>
    /// 设置是否为设计模式
    /// </summary>
    /// <param name="element"></param>
    /// <param name="value"></param>
    public static void SetIsDesignerMode(UIElement element, bool value)
    {
        element.SetValue(IsDesignerModeProperty, value);
    }

    private static void OnIsDesignerModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        if ((bool)e.NewValue)
        {
            var myAdornerLayer = AdornerLayer.GetAdornerLayer(element);
            myAdornerLayer?.Add(new CanvasAdorner(element));
        }
        else
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(element);
            var adorners = adornerLayer?.GetAdorners(element);
            if (adorners == null) return;
            foreach (var adorner in adorners)
            {
                adornerLayer?.Remove(adorner);
            }
        }
    }
}