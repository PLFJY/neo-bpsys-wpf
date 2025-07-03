using neo_bpsys_wpf.Controls;
using System.Windows;
using System.Windows.Documents;

namespace neo_bpsys_wpf.AttachedBehaviors;

public static class DesignBehavior
{
    public static readonly DependencyProperty IsDesignModeProperty =
        DependencyProperty.RegisterAttached("IsDesignMode", typeof(bool), typeof(DesignBehavior),
            new PropertyMetadata(false, OnIsDesignModeChanged));

    public static bool GetIsDesignMode(UIElement element)
    {
        return (bool)element.GetValue(IsDesignModeProperty);
    }

    public static void SetIsDesignMode(UIElement element, bool value)
    {
        element.SetValue(IsDesignModeProperty, value);
    }

    private static void OnIsDesignModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
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
}