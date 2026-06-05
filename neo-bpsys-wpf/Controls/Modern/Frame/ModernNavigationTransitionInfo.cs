#nullable enable

using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

// Inspired by iNKORE.UI.WPF.Modern NavigationTransitionInfo.
// This local version keeps only the transition primitives needed by ModernFrame.
public abstract class ModernNavigationTransitionInfo : DependencyObject
{
    static ModernNavigationTransitionInfo()
    {
        AccelerateKeySpline = new KeySpline(0.7, 0, 1, 0.5);
        AccelerateKeySpline.Freeze();

        DecelerateKeySpline = new KeySpline(0.1, 0.9, 0.2, 1);
        DecelerateKeySpline.Freeze();
    }

    internal virtual Storyboard? CreateEnterStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        return null;
    }

    internal virtual Storyboard? CreateExitStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        return null;
    }

    internal static readonly KeySpline AccelerateKeySpline;
    internal static readonly KeySpline DecelerateKeySpline;

    internal static readonly PropertyPath OpacityPath = new(UIElement.OpacityProperty);
    internal static readonly PropertyPath TranslateXPath = new("(UIElement.RenderTransform).(TranslateTransform.X)");
    internal static readonly PropertyPath TranslateYPath = new("(UIElement.RenderTransform).(TranslateTransform.Y)");
}
