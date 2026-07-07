using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.ProductTour.Controls;

internal static class OverlayHost
{
    public static Panel GetHostPanel(FrameworkElement owner)
    {
        if (owner is Window window)
        {
            if (window.Content is Panel panel)
            {
                return panel;
            }

            var original = window.Content as UIElement;
            var grid = new Grid();
            window.Content = null;
            if (original != null)
            {
                grid.Children.Add(original);
            }

            window.Content = grid;
            return grid;
        }

        if (owner is Panel ownerPanel)
        {
            return ownerPanel;
        }

        var current = owner.Parent;
        while (current is not null)
        {
            if (current is Panel panel)
            {
                return panel;
            }

            current = current is FrameworkElement element ? element.Parent : null;
        }

        throw new InvalidOperationException("Unable to locate an overlay host panel.");
    }

    public static Task FadeOutAndRemoveAsync(Panel host, UIElement overlay, TimeSpan duration)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation(0, duration)
        {
            From = overlay.Opacity,
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            host.Children.Remove(overlay);
            source.TrySetResult();
        };
        overlay.BeginAnimation(UIElement.OpacityProperty, animation);
        return source.Task;
    }
}
