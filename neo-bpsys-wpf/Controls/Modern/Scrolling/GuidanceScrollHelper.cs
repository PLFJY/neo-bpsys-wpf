using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class GuidanceScrollHelper
{
    public static bool IsTargetMatch(FrameworkElement target, HighlightMessage message)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(message);

        if (message.GameAction is null)
        {
            return false;
        }

        var targetAction = GuidanceScrollTarget.GetAction(target);
        if (targetAction != message.GameAction)
        {
            return false;
        }

        var targetIndex = GuidanceScrollTarget.GetIndex(target);
        if (targetIndex is null)
        {
            return true;
        }

        return message.Index?.Contains(targetIndex.Value) == true;
    }

    public static FrameworkElement? FindBestTarget(DependencyObject scope, HighlightMessage message)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(message);

        if (message.GameAction is null)
        {
            return null;
        }

        var candidates = FindTargetDescendants(scope)
            .Where(target => IsTargetMatch(target, message))
            .Select((target, order) => new TargetCandidate(
                target,
                order,
                GetRequestedIndexOrder(target, message),
                IsVisibleAndEnabled(target)))
            .OrderBy(candidate => candidate.RequestedIndexOrder)
            .ThenByDescending(candidate => candidate.IsVisibleAndEnabled)
            .ThenBy(candidate => candidate.VisualTreeOrder);

        return candidates.FirstOrDefault()?.Target;
    }

    public static void ScrollElementIntoView(FrameworkElement target, double topMargin = 80, bool animated = true)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.Dispatcher.CheckAccess())
        {
            target.Dispatcher.Invoke(() => ScrollElementIntoView(target, topMargin, animated));
            return;
        }

        if (!target.IsLoaded || !IsConnectedToPresentationSource(target))
        {
            return;
        }

        var scrollViewer = ScrollViewerSearchHelper.FindNearestScrollableAncestor(target);
        if (scrollViewer is null)
        {
            TryBringIntoView(target);
            return;
        }

        try
        {
            var point = target.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
            var targetOffset = scrollViewer.VerticalOffset + point.Y - topMargin;
            var clampedOffset = ScrollAnimationHelper.ClampVerticalOffset(scrollViewer, targetOffset);

            if (SmoothScrollBehavior.GetIsProgrammaticAnimationEnabled(scrollViewer))
            {
                ScrollAnimationHelper.SmoothScrollToVerticalOffset(scrollViewer, clampedOffset, animated: animated);
            }
            else
            {
                scrollViewer.ScrollToVerticalOffset(clampedOffset);
            }
        }
        catch (InvalidOperationException)
        {
            TryBringIntoView(target);
        }
        catch (ArgumentException)
        {
            TryBringIntoView(target);
        }
    }

    private static IEnumerable<FrameworkElement> FindTargetDescendants(DependencyObject scope)
    {
        var visited = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(scope);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current is FrameworkElement element && GuidanceScrollTarget.GetAction(element) is not null)
            {
                yield return element;
            }

            foreach (var child in GetChildren(current).Reverse())
            {
                stack.Push(child);
            }
        }
    }

    private static IEnumerable<DependencyObject> GetChildren(DependencyObject current)
    {
        var visualChildren = GetVisualChildren(current).ToList();
        if (visualChildren.Count > 0)
        {
            return visualChildren;
        }

        return LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>();
    }

    private static IEnumerable<DependencyObject> GetVisualChildren(DependencyObject current)
    {
        if (current is not Visual && current is not Visual3D)
        {
            yield break;
        }

        var count = 0;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(current);
        }
        catch (InvalidOperationException)
        {
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            DependencyObject? child = null;
            try
            {
                child = VisualTreeHelper.GetChild(current, i);
            }
            catch (InvalidOperationException)
            {
                yield break;
            }

            if (child is not null)
            {
                yield return child;
            }
        }
    }

    private static int GetRequestedIndexOrder(FrameworkElement target, HighlightMessage message)
    {
        var targetIndex = GuidanceScrollTarget.GetIndex(target);
        if (targetIndex is null || message.Index is null)
        {
            return int.MaxValue;
        }

        var order = message.Index.IndexOf(targetIndex.Value);
        return order >= 0 ? order : int.MaxValue;
    }

    private static bool IsVisibleAndEnabled(FrameworkElement target) =>
        target.IsEnabled
        && target.Visibility == Visibility.Visible
        && (!target.IsLoaded || target.IsVisible);

    private static bool IsConnectedToPresentationSource(FrameworkElement target) =>
        PresentationSource.FromVisual(target) is not null;

    private static void TryBringIntoView(FrameworkElement target)
    {
        try
        {
            target.BringIntoView();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record TargetCandidate(
        FrameworkElement Target,
        int VisualTreeOrder,
        int RequestedIndexOrder,
        bool IsVisibleAndEnabled);
}
