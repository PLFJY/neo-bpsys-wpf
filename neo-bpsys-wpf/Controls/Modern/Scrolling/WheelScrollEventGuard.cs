using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

internal static class WheelScrollEventGuard
{
    public static bool ShouldSkipSmoothScroll(ScrollViewer owner, MouseWheelEventArgs e)
    {
        return ShouldSkipSmoothScroll(owner, e, explicitSource: null);
    }

    internal static bool ShouldSkipSmoothScroll(ScrollViewer owner, MouseWheelEventArgs e, DependencyObject? explicitSource)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return true;
        }

        return ShouldSuppressOwnerScroll(owner, e, explicitSource);
    }

    public static bool ShouldSuppressOwnerScroll(ScrollViewer owner, MouseWheelEventArgs e)
    {
        return ShouldSuppressOwnerScroll(owner, e, explicitSource: null);
    }

    internal static bool ShouldSuppressOwnerScroll(ScrollViewer owner, MouseWheelEventArgs e, DependencyObject? explicitSource)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled)
        {
            return true;
        }

        foreach (var source in GetCandidateSources(e, explicitSource))
        {
            if (source is null)
            {
                continue;
            }

            if (IsInsideOpenComboBox(source)
                || IsInsidePopupTree(source)
                || IsInsideNestedScrollableOwner(owner, source))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideOpenComboBox(DependencyObject source)
    {
        foreach (var ancestor in EnumerateAncestorsAndSelf(source))
        {
            if (ancestor is ComboBox { IsDropDownOpen: true })
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsidePopupTree(DependencyObject source)
    {
        foreach (var ancestor in EnumerateAncestorsAndSelf(source))
        {
            if (ancestor is Popup or ContextMenu)
            {
                return true;
            }

            if (ancestor.GetType().Name.Contains("PopupRoot", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideNestedScrollableOwner(ScrollViewer owner, DependencyObject source)
    {
        foreach (var ancestor in EnumerateAncestorsAndSelf(source))
        {
            if (ReferenceEquals(ancestor, owner))
            {
                return false;
            }

            if (ancestor is ComboBox { IsDropDownOpen: true })
            {
                return true;
            }

            if (IsNestedScrollableElement(ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNestedScrollableElement(DependencyObject element)
    {
        if (element is ScrollViewer or ListBox or ListView or DataGrid or TreeView)
        {
            return true;
        }

        var typeName = element.GetType().Name;
        return typeName.Contains("DynamicScrollViewer", StringComparison.Ordinal);
    }

    private static IEnumerable<DependencyObject?> GetCandidateSources(MouseWheelEventArgs e, DependencyObject? explicitSource)
    {
        yield return explicitSource;
        yield return e.OriginalSource as DependencyObject;
        yield return e.Source as DependencyObject;
        yield return Mouse.DirectlyOver as DependencyObject;
        yield return Keyboard.FocusedElement as DependencyObject;
    }

    private static IEnumerable<DependencyObject> EnumerateAncestorsAndSelf(DependencyObject source)
    {
        var stack = new Stack<DependencyObject>();
        var visited = new HashSet<DependencyObject>();
        stack.Push(source);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            yield return current;

            foreach (var parent in GetRelatedParents(current))
            {
                if (parent is not null && !visited.Contains(parent))
                {
                    stack.Push(parent);
                }
            }
        }
    }

    private static IEnumerable<DependencyObject?> GetRelatedParents(DependencyObject current)
    {
        yield return TryGetVisualParent(current);
        yield return TryGetLogicalParent(current);

        if (current is FrameworkElement frameworkElement)
        {
            yield return frameworkElement.TemplatedParent;
        }
        else if (current is FrameworkContentElement frameworkContentElement)
        {
            yield return frameworkContentElement.TemplatedParent;
        }

        if (current is Popup popup)
        {
            yield return popup.PlacementTarget;
        }
        else if (current is ContextMenu contextMenu)
        {
            yield return contextMenu.PlacementTarget;
        }
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

    private static DependencyObject? TryGetLogicalParent(DependencyObject current)
    {
        try
        {
            return LogicalTreeHelper.GetParent(current);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
