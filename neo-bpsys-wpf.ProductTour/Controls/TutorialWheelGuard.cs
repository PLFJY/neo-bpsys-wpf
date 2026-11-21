using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// 在教程浮层显示期间拦截所属窗口的滚轮事件，避免目标控件位于遮罩镂空区域时滚动后台页面。
/// </summary>
internal static class TutorialWheelGuard
{
    private static readonly ConditionalWeakTable<FrameworkElement, GuardState> States = new();

    /// <summary>
    /// 为指定教程浮层挂接窗口级滚轮拦截。
    /// </summary>
    /// <param name="overlay">教程浮层根元素。</param>
    public static void Attach(FrameworkElement overlay)
    {
        var state = States.GetOrCreateValue(overlay);
        overlay.Loaded += (_, _) => state.Attach(overlay);
        overlay.Unloaded += (_, _) => state.Detach();
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private sealed class GuardState
    {
        private Window? _window;
        private MouseWheelEventHandler? _handler;

        public void Attach(FrameworkElement overlay)
        {
            Detach();
            _window = Window.GetWindow(overlay);
            if (_window is null)
            {
                return;
            }

            _handler = OnPreviewMouseWheel;
            _window.AddHandler(
                UIElement.PreviewMouseWheelEvent,
                _handler,
                handledEventsToo: true);
        }

        public void Detach()
        {
            if (_window is null)
            {
                return;
            }

            _window.RemoveHandler(
                UIElement.PreviewMouseWheelEvent,
                _handler!);
            _window = null;
            _handler = null;
        }
    }
}
