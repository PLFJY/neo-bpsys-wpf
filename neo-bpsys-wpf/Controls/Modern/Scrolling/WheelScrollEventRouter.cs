using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

internal sealed class WheelScrollEventRouter
{
    private readonly ScrollViewer _owner;
    private readonly Func<bool> _isSmoothScrollingEnabled;
    private readonly Func<double> _wheelScrollMultiplier;
    private readonly Func<int> _scrollAnimationDuration;
    private readonly Func<IEasingFunction?> _scrollEasingFunction;
    private Window? _window;
    private bool _isWindowHandlerAttached;

    public WheelScrollEventRouter(
        ScrollViewer owner,
        Func<bool> isSmoothScrollingEnabled,
        Func<double> wheelScrollMultiplier,
        Func<int> scrollAnimationDuration,
        Func<IEasingFunction?> scrollEasingFunction)
    {
        _owner = owner;
        _isSmoothScrollingEnabled = isSmoothScrollingEnabled;
        _wheelScrollMultiplier = wheelScrollMultiplier;
        _scrollAnimationDuration = scrollAnimationDuration;
        _scrollEasingFunction = scrollEasingFunction;
    }

    public void Attach()
    {
        var window = Window.GetWindow(_owner);
        if (ReferenceEquals(window, _window) && _isWindowHandlerAttached)
        {
            return;
        }

        DetachWindowHandler();
        _window = window;

        if (_window is null)
        {
            return;
        }

        _window.PreviewMouseWheel += OnWindowPreviewMouseWheel;
        _isWindowHandlerAttached = true;
    }

    public void Detach()
    {
        DetachWindowHandler();
        _window = null;
    }

    private void DetachWindowHandler()
    {
        if (!_isWindowHandlerAttached || _window is null)
        {
            return;
        }

        _window.PreviewMouseWheel -= OnWindowPreviewMouseWheel;
        _isWindowHandlerAttached = false;
    }

    private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || !_isSmoothScrollingEnabled())
        {
            return;
        }

        var hoverSource = Mouse.DirectlyOver as DependencyObject;
        if (!WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(_owner, e, hoverSource))
        {
            return;
        }

        ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
            _owner,
            e,
            _wheelScrollMultiplier(),
            _scrollAnimationDuration(),
            isSmoothScrollingEnabled: true,
            easingFunction: _scrollEasingFunction(),
            explicitSource: hoverSource);
    }
}
