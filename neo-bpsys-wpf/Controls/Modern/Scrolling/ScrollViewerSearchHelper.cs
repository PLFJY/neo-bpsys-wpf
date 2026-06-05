using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class ScrollViewerSearchHelper
{
    public static ScrollViewer? FindNearestScrollableAncestor(DependencyObject? target)
    {
        var current = GetParent(target);
        var visited = new HashSet<DependencyObject>();

        while (current is not null && visited.Add(current))
        {
            if (current is ScrollViewer scrollViewer
                && (scrollViewer.ScrollableHeight > 0 || scrollViewer.ScrollableWidth > 0))
            {
                return scrollViewer;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject? current)
    {
        if (current is null)
        {
            return null;
        }

        var visualParent = TryGetVisualParent(current);
        if (visualParent is not null)
        {
            return visualParent;
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static DependencyObject? TryGetVisualParent(DependencyObject current)
    {
        if (current is not Visual && current is not Visual3D)
        {
            return null;
        }

        try
        {
            return VisualTreeHelper.GetParent(current);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
