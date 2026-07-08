using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ProductTour;

internal static class TargetElementFinder
{
    public static Task<FrameworkElement?> FindAsync(
        FrameworkElement owner,
        ProductTourStep step,
        CancellationToken cancellationToken)
    {
        return step.TargetKind switch
        {
            TutorialTargetKind.Name when !string.IsNullOrWhiteSpace(step.TargetName) =>
                FindByNameAsync(owner, step.TargetName, step.Timeout, cancellationToken),
            TutorialTargetKind.NavigationItem when !string.IsNullOrWhiteSpace(step.TargetKey) =>
                FindNavigationItemAsync(owner, step.TargetKey, step.Timeout, cancellationToken),
            TutorialTargetKind.DescendantType when !string.IsNullOrWhiteSpace(step.TargetKey) =>
                FindDescendantTypeAsync(owner, step.TargetName, step.TargetKey, step.Timeout, cancellationToken),
            TutorialTargetKind.ElementTag when !string.IsNullOrWhiteSpace(step.TargetKey) =>
                FindByElementTagAsync(owner, step.TargetKey, step.Timeout, cancellationToken),
            _ => Task.FromResult<FrameworkElement?>(null)
        };
    }

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
                await BringTargetIntoViewAsync(owner, result, cancellationToken);
                return result;
            }

            await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }

        return null;
    }

    public static async Task<FrameworkElement?> FindByElementTagAsync(
        FrameworkElement owner,
        string targetTag,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = FindElementByTag(owner, targetTag);
            if (result != null && result.IsLoaded)
            {
                await BringTargetIntoViewAsync(owner, result, cancellationToken);
                return result;
            }

            await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }

        return null;
    }

    public static async Task<FrameworkElement?> FindDescendantTypeAsync(
        FrameworkElement owner,
        string? hostTargetName,
        string targetTypeFullName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FrameworkElement? host = owner;
            if (!string.IsNullOrWhiteSpace(hostTargetName))
            {
                host = owner.FindName(hostTargetName) as FrameworkElement
                    ?? FindVisualChild(owner, hostTargetName);
            }

            if (host != null)
            {
                var result = FindDescendantByType(host, targetTypeFullName);
                if (result != null && result.IsLoaded)
                {
                    await BringTargetIntoViewAsync(owner, result, cancellationToken);
                    return result;
                }
            }

            await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }

        return null;
    }

    public static async Task<FrameworkElement?> FindNavigationItemAsync(
        FrameworkElement owner,
        string targetKey,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = FindNavigationItem(owner, targetKey);
            if (result != null && result.IsLoaded)
            {
                await BringTargetIntoViewAsync(owner, result, cancellationToken);
                return result;
            }

            await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }

        return null;
    }

    private static FrameworkElement? FindDescendantByType(DependencyObject root, string targetTypeFullName)
    {
        if (root is FrameworkElement element
            && string.Equals(element.GetType().FullName, targetTypeFullName, StringComparison.Ordinal))
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var nested = FindDescendantByType(child, targetTypeFullName);
            if (nested != null)
            {
                return nested;
            }
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            return FindDescendantByType(content, targetTypeFullName);
        }

        return null;
    }

    private static async Task BringTargetIntoViewAsync(
        FrameworkElement owner,
        FrameworkElement target,
        CancellationToken cancellationToken)
    {
        target.BringIntoView();
        target.UpdateLayout();
        owner.UpdateLayout();
        await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded, cancellationToken);
        target.UpdateLayout();
        owner.UpdateLayout();
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

    private static FrameworkElement? FindElementByTag(DependencyObject root, string targetTag)
    {
        if (root is FrameworkElement element
            && element.Tag is string tag
            && string.Equals(tag, targetTag, StringComparison.Ordinal))
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var nested = FindElementByTag(child, targetTag);
            if (nested != null)
            {
                return nested;
            }
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            return FindElementByTag(content, targetTag);
        }

        return null;
    }

    private static FrameworkElement? FindNavigationItem(DependencyObject root, string targetKey)
    {
        if (root is FrameworkElement element && IsNavigationItemMatch(element, targetKey))
        {
            return FindClickableDescendant(element) ?? FindClickableAncestor(element) ?? element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var nested = FindNavigationItem(child, targetKey);
            if (nested != null)
            {
                return nested;
            }
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            return FindNavigationItem(content, targetKey);
        }

        return null;
    }

    private static bool IsNavigationItemMatch(FrameworkElement element, string targetKey)
    {
        if (MatchesNavigationCandidate(element, targetKey))
        {
            return true;
        }

        if (MatchesNavigationCandidate(element.DataContext, targetKey))
        {
            return true;
        }

        return element is ContentControl contentControl
            && MatchesNavigationCandidate(contentControl.Content, targetKey);
    }

    private static FrameworkElement? FindClickableDescendant(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button)
            {
                return button;
            }

            var nested = FindClickableDescendant(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static FrameworkElement? FindClickableAncestor(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is Button button)
            {
                return button;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool MatchesNavigationCandidate(object? candidate, string targetKey)
    {
        if (candidate is null)
        {
            return false;
        }

        if (TryGetProperty(candidate, "TargetPageType") is Type targetPageType
            && string.Equals(targetPageType.FullName, targetKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (TryGetProperty(candidate, "Tag") is string tag
            && string.Equals(tag, targetKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (TryGetProperty(candidate, "TargetPageTag") is string targetPageTag
            && string.Equals(targetPageTag, targetKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (TryGetProperty(candidate, "Id") is string id
            && string.Equals(id, targetKey, StringComparison.Ordinal))
        {
            return true;
        }

        return IsFallbackTextMatch(TryGetProperty(candidate, "Content"), targetKey)
            || IsFallbackTextMatch(TryGetProperty(candidate, "Header"), targetKey);
    }

    private static object? TryGetProperty(object candidate, string propertyName)
    {
        var property = candidate.GetType().GetProperty(propertyName);
        return property?.GetValue(candidate);
    }

    private static bool IsFallbackTextMatch(object? value, string targetKey) =>
        value is string text && string.Equals(text, targetKey, StringComparison.Ordinal);
}
