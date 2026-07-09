using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Provides generic tutorial step action helpers.
/// </summary>
public static class TutorialStepActions
{
    /// <summary>
    /// Creates a delay action.
    /// </summary>
    /// <param name="milliseconds">Delay duration in milliseconds.</param>
    /// <returns>The action definition.</returns>
    public static TutorialStepAction Delay(int milliseconds) =>
        Delay(TimeSpan.FromMilliseconds(milliseconds));

    /// <summary>
    /// Creates a delay action.
    /// </summary>
    /// <param name="duration">Delay duration.</param>
    /// <returns>The action definition.</returns>
    public static TutorialStepAction Delay(TimeSpan duration) =>
        new($"Delay({duration.TotalMilliseconds:0}ms)", (_, cancellationToken) => Task.Delay(duration, cancellationToken));

    /// <summary>
    /// Creates an action that scrolls a named element into view with a simple smooth vertical animation.
    /// </summary>
    /// <param name="targetName">Target element name.</param>
    /// <param name="durationMs">Scroll animation duration in milliseconds.</param>
    /// <returns>The action definition.</returns>
    public static TutorialStepAction SmoothScrollTo(string targetName, int durationMs = 350) =>
        new($"SmoothScrollTo({targetName})", async (context, cancellationToken) =>
        {
            var target = FindNamedElement(context.Owner, targetName)
                ?? throw new InvalidOperationException($"Tutorial scroll target '{targetName}' was not found.");
            await SmoothScrollIntoViewAsync(
                context.Owner,
                target,
                TimeSpan.FromMilliseconds(durationMs),
                cancellationToken);
        });

    /// <summary>
    /// Creates an action that waits until the owner dispatcher reaches idle priority.
    /// </summary>
    /// <returns>The action definition.</returns>
    public static TutorialStepAction WaitForDispatcherIdle() =>
        new("WaitForDispatcherIdle", async (context, cancellationToken) =>
        {
            await context.Owner.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ContextIdle,
                cancellationToken);
        });

    /// <summary>
    /// Creates an action that activates the owner window.
    /// </summary>
    /// <returns>The action definition.</returns>
    public static TutorialStepAction ActivateOwnerWindow() =>
        new("ActivateOwnerWindow", (context, _) =>
        {
            var window = context.Owner as Window ?? Window.GetWindow(context.Owner);
            window?.Activate();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Returns an optional copy of an action.
    /// </summary>
    /// <param name="action">Action to mark optional.</param>
    /// <returns>The optional action definition.</returns>
    public static TutorialStepAction Optional(TutorialStepAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TutorialStepAction(action.Name, action.ExecuteAsync) { IsOptional = true };
    }

    private static async Task SmoothScrollIntoViewAsync(
        FrameworkElement owner,
        FrameworkElement target,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var scrollViewer = FindAncestorOrDescendantScrollViewer(target)
            ?? FindAncestorOrDescendantScrollViewer(owner);
        if (scrollViewer == null)
        {
            target.BringIntoView();
            await WaitForLayoutAsync(owner, target, cancellationToken);
            return;
        }

        await owner.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded, cancellationToken);
        owner.UpdateLayout();
        target.UpdateLayout();

        Point targetPoint;
        try
        {
            targetPoint = target.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
        }
        catch (InvalidOperationException)
        {
            target.BringIntoView();
            await WaitForLayoutAsync(owner, target, cancellationToken);
            return;
        }

        var desiredOffset = Math.Clamp(
            scrollViewer.VerticalOffset + targetPoint.Y - 24,
            0,
            scrollViewer.ScrollableHeight);
        var startOffset = scrollViewer.VerticalOffset;
        var frameCount = Math.Max(1, (int)Math.Ceiling(duration.TotalMilliseconds / 16));

        for (var frame = 1; frame <= frameCount; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var progress = (double)frame / frameCount;
            var eased = 1 - Math.Pow(1 - progress, 3);
            scrollViewer.ScrollToVerticalOffset(startOffset + (desiredOffset - startOffset) * eased);
            await owner.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken);
        }

        scrollViewer.ScrollToVerticalOffset(desiredOffset);
        await WaitForLayoutAsync(owner, target, cancellationToken);
    }

    private static async Task WaitForLayoutAsync(
        FrameworkElement owner,
        FrameworkElement target,
        CancellationToken cancellationToken)
    {
        await owner.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, cancellationToken);
        target.UpdateLayout();
        owner.UpdateLayout();
    }

    private static FrameworkElement? FindNamedElement(DependencyObject root, string targetName)
    {
        if (root is FrameworkElement element)
        {
            if (string.Equals(element.Name, targetName, StringComparison.Ordinal))
            {
                return element;
            }

            if (element.FindName(targetName) is FrameworkElement namedElement)
            {
                return namedElement;
            }
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var nested = FindNamedElement(VisualTreeHelper.GetChild(root, i), targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return root is ContentControl { Content: DependencyObject content }
            ? FindNamedElement(content, targetName)
            : null;
    }

    private static ScrollViewer? FindAncestorOrDescendantScrollViewer(DependencyObject root)
    {
        var current = root;
        while (current != null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return FindDescendantScrollViewer(root);
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var nested = FindDescendantScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (nested != null)
            {
                return nested;
            }
        }

        return root is ContentControl { Content: DependencyObject content }
            ? FindDescendantScrollViewer(content)
            : null;
    }
}
