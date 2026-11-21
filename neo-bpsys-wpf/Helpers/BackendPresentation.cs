using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Frame;

namespace neo_bpsys_wpf.Helpers;

/// <summary>在嵌套导航宿主均完成呈现后启动页面的非关键工作。</summary>
internal static class BackendPresentation
{
    internal static void Attach(FrameworkElement view, Action action)
    {
        var frames = new List<ModernFrame>();
        DispatcherOperation? pending = null;
        void Detach()
        {
            pending?.Abort();
            pending = null;
            foreach (var frame in frames) frame.NavigationCompleted -= Completed;
            frames.Clear();
        }
        void Check()
        {
            // WPF 的 Unloaded 通知可能晚于实际脱离视觉树，不能只依赖 IsLoaded。
            if (!view.IsLoaded || !view.IsVisible || PresentationSource.FromVisual(view) is null
                || frames.Any(frame => frame.IsNavigationPending)) return;
            var ancestors = new HashSet<DependencyObject>();
            for (DependencyObject? node = view; node is not null; node = VisualTreeHelper.GetParent(node))
                ancestors.Add(node);
            if (frames.Any(frame => !ancestors.Contains(frame)
                || frame.CurrentContent is null || !ancestors.Contains(frame.CurrentContent))) return;
            Detach();
            action();
        }
        void Completed(object? sender, ModernFrameNavigationEventArgs args) => Check();
        view.Loaded += (_, _) =>
        {
            Detach();
            for (DependencyObject? node = view; node is not null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is not ModernFrame frame) continue;
                frames.Add(frame);
                frame.NavigationCompleted += Completed;
            }
            pending = view.Dispatcher.BeginInvoke(() => { pending = null; Check(); }, DispatcherPriority.Loaded);
        };
        view.Unloaded += (_, _) => Detach();
    }
}
