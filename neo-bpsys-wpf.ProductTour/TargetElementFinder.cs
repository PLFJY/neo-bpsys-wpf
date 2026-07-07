using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ProductTour;

internal static class TargetElementFinder
{
    public static async Task<FrameworkElement?> FindByNameAsync(
        FrameworkElement owner,
        string targetName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = owner.FindName(targetName) as FrameworkElement
                ?? FindVisualChild(owner, targetName);
            if (result != null && result.IsLoaded)
            {
                result.BringIntoView();
                return result;
            }

            await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }

        return null;
    }

    private static FrameworkElement? FindVisualChild(DependencyObject root, string targetName)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element && element.Name == targetName)
            {
                return element;
            }

            var nested = FindVisualChild(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            return FindVisualChild(content, targetName);
        }

        return null;
    }
}
