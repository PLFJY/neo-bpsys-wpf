using System.Windows;
using System.Windows.Documents;
using neo_bpsys_wpf.Core.Controls;

namespace neo_bpsys_wpf.Core.AttachedBehaviors;

public static class DesignBehavior
{
    public static readonly DependencyProperty IsDesignerModeProperty =
        DependencyProperty.RegisterAttached("IsDesignerMode", typeof(bool), typeof(DesignBehavior),
            new PropertyMetadata(false, OnIsDesignerModeChanged));

    public static bool GetIsDesignerMode(UIElement element)
    {
        return (bool)element.GetValue(IsDesignerModeProperty);
    }

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