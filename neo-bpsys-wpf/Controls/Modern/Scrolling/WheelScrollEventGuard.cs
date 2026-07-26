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
        return ShouldSkipSmoothScroll(owner, e, explicitSource, respectExplicitSelfOwnership: true);
    }

    internal static bool ShouldSkipSmoothScroll(
        ScrollViewer owner,
        MouseWheelEventArgs e,
        DependencyObject? explicitSource,
        bool respectExplicitSelfOwnership)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return true;
        }

        return ShouldSuppressOwnerScroll(owner, e, explicitSource, respectExplicitSelfOwnership);
    }

    public static bool ShouldSuppressOwnerScroll(ScrollViewer owner, MouseWheelEventArgs e)
    {
        return ShouldSuppressOwnerScroll(owner, e, explicitSource: null);
    }

    public static bool ShouldOwnerHandleHoverWheel(ScrollViewer owner, MouseWheelEventArgs e)
    {
        return ShouldOwnerHandleHoverWheel(owner, e, explicitHoverSource: null);
    }

    internal static bool ShouldOwnerHandleHoverWheel(ScrollViewer owner, MouseWheelEventArgs e, DependencyObject? explicitHoverSource)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled)
        {
            return false;
        }

        var hoverSource = explicitHoverSource
            ?? Mouse.DirectlyOver as DependencyObject
            ?? e.OriginalSource as DependencyObject;

        if (hoverSource is null)
        {
            return false;
        }

        if (IsOpenComboBoxCandidate(e, hoverSource)
            || IsInsidePopupTree(hoverSource)
            || IsInsideExplicitSelfScrollRegion(owner, hoverSource))
        {
            return false;
        }

        return IsRelatedDescendantOf(owner, hoverSource);
    }

    internal static bool ShouldSuppressOwnerScroll(ScrollViewer owner, MouseWheelEventArgs e, DependencyObject? explicitSource)
    {
        return ShouldSuppressOwnerScroll(owner, e, explicitSource, respectExplicitSelfOwnership: true);
    }

    internal static bool ShouldSuppressOwnerScroll(
        ScrollViewer owner,
        MouseWheelEventArgs e,
        DependencyObject? explicitSource,
        bool respectExplicitSelfOwnership)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled)
        {
            return true;
        }

        if (Keyboard.FocusedElement is DependencyObject focusedElement
            && IsInsideOpenComboBox(focusedElement))
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
                || (respectExplicitSelfOwnership && IsInsideExplicitSelfScrollRegion(owner, source)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOpenComboBoxCandidate(MouseWheelEventArgs e, DependencyObject hoverSource)
    {
        if (IsInsideOpenComboBox(hoverSource))
        {
            return true;
        }

        foreach (var source in GetNonHoverCandidateSources(e))
        {
            if (source is not null && IsInsideOpenComboBox(source))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsInsidePopupOrOpenComboBox(DependencyObject source) =>
        IsInsideOpenComboBox(source) || IsInsidePopupTree(source);

    private static bool IsRelatedDescendantOf(DependencyObject owner, DependencyObject source)
    {
        foreach (var ancestor in EnumerateAncestorsAndSelf(source))
        {
            if (ReferenceEquals(ancestor, owner))
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

    private static bool IsInsideExplicitSelfScrollRegion(ScrollViewer owner, DependencyObject source)
    {
        foreach (var ancestor in EnumerateAncestorsAndSelf(source))
        {
            if (ReferenceEquals(ancestor, owner))
            {
                return false;
            }

            var ownership = ModernScroll.GetOwnership(ancestor);
            if (ownership == ModernScrollOwnership.Self)
            {
                return true;
            }

            if (ownership == ModernScrollOwnership.Frame)
            {
                return false;
            }
        }

        return false;
    }

    private static IEnumerable<DependencyObject?> GetCandidateSources(MouseWheelEventArgs e, DependencyObject? explicitSource)
    {
        yield return explicitSource;
        yield return e.OriginalSource as DependencyObject;
        yield return e.Source as DependencyObject;
        yield return Mouse.DirectlyOver as DependencyObject;
    }

    private static IEnumerable<DependencyObject?> GetNonHoverCandidateSources(MouseWheelEventArgs e)
    {
        yield return e.OriginalSource as DependencyObject;
        yield return e.Source as DependencyObject;
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
