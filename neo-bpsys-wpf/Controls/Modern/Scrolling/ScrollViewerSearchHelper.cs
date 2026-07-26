using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 提供在可视化树中查找最近可滚动 <see cref="ScrollViewer"/> 祖先的辅助方法。
/// </summary>
public static class ScrollViewerSearchHelper
{
    /// <summary>
    /// 在可视化树中查找指定元素最近的可滚动祖先 <see cref="ScrollViewer"/>。
    /// </summary>
    /// <param name="target">要开始搜索的元素，可以为 <c>null</c>。</param>
    /// <returns>最近的可滚动 <see cref="ScrollViewer"/>，如果未找到则为 <c>null</c>。</returns>
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
