using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 提供通用的教程步骤动作辅助方法。
/// </summary>
public static class TutorialStepActions
{
    /// <summary>
    /// 创建一个延迟动作。
    /// </summary>
    /// <param name="milliseconds">延迟时长（毫秒）。</param>
    /// <returns>动作定义。</returns>
    public static TutorialStepAction Delay(int milliseconds) =>
        Delay(TimeSpan.FromMilliseconds(milliseconds));

    /// <summary>
    /// 创建一个延迟动作。
    /// </summary>
    /// <param name="duration">延迟时长。</param>
    /// <returns>动作定义。</returns>
    public static TutorialStepAction Delay(TimeSpan duration) =>
        new($"Delay({duration.TotalMilliseconds:0}ms)", (_, cancellationToken) => Task.Delay(duration, cancellationToken));

    /// <summary>
    /// 创建一个将命名元素滚动到可视区域、带有简单平滑垂直动画的动作。
    /// </summary>
    /// <param name="targetName">目标元素名称。</param>
    /// <param name="durationMs">滚动动画时长（毫秒）。</param>
    /// <returns>动作定义。</returns>
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
    /// 创建一个等待宿主 dispatcher 到达空闲优先级的动作。
    /// </summary>
    /// <returns>动作定义。</returns>
    public static TutorialStepAction WaitForDispatcherIdle() =>
        new("WaitForDispatcherIdle", async (context, cancellationToken) =>
        {
            await context.Owner.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ContextIdle,
                cancellationToken);
        });

    /// <summary>
    /// 创建一个激活宿主窗口的动作。
    /// </summary>
    /// <returns>动作定义。</returns>
    public static TutorialStepAction ActivateOwnerWindow() =>
        new("ActivateOwnerWindow", (context, _) =>
        {
            var window = context.Owner as Window ?? Window.GetWindow(context.Owner);
            window?.Activate();
            return Task.CompletedTask;
        });

    /// <summary>
    /// 创建一个软等待动作，轮询断言直到其返回 true 或超时。
    /// 超时后直接返回而不抛出异常，因此可与 <see cref="ProductTourStep.AllowMissingTarget"/> 配合使用。
    /// </summary>
    /// <param name="name">在日志中显示的诊断名称。</param>
    /// <param name="predicate">在步骤动作上下文上求值的断言。当等待的条件满足时返回 true。</param>
    /// <param name="timeout">最大等待时长。</param>
    /// <param name="pollInterval">断言检查之间的间隔。默认为 80 毫秒。</param>
    /// <returns>动作定义。</returns>
    public static TutorialStepAction WaitUntil(
        string name,
        Func<TutorialStepActionContext, bool> predicate,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(80);
        return new TutorialStepAction($"WaitUntil({name})", async (context, cancellationToken) =>
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (predicate(context))
                {
                    return;
                }

                await Task.Delay(interval, cancellationToken);
            }
        });
    }

    /// <summary>
    /// 返回某个动作的可选副本。
    /// </summary>
    /// <param name="action">要标记为可选的动作。</param>
    /// <returns>可选动作定义。</returns>
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
