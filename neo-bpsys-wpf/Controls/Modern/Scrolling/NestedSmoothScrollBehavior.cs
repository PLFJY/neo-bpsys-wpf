using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 为嵌套内容提供平滑滚轮滚动行为的附加属性，适用于包含内部 <see cref="ScrollViewer"/> 的 <see cref="UIElement"/>。
/// </summary>
public static class NestedSmoothScrollBehavior
{
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(NestedSmoothScrollState),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// <see cref="IsEnabledProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// <see cref="DurationProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.RegisterAttached(
            "Duration",
            typeof(int),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata((int)ScrollAnimationHelper.DefaultDuration.TotalMilliseconds));

    /// <summary>
    /// <see cref="WheelMultiplierProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty WheelMultiplierProperty =
        DependencyProperty.RegisterAttached(
            "WheelMultiplier",
            typeof(double),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(1.0));

    /// <summary>
    /// <see cref="UseSmoothScrollingProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty UseSmoothScrollingProperty =
        DependencyProperty.RegisterAttached(
            "UseSmoothScrolling",
            typeof(bool),
            typeof(NestedSmoothScrollBehavior),
            new PropertyMetadata(true));

    /// <summary>
    /// 获取指定元素的 <see cref="IsEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>如果启用了嵌套平滑滚动则为 <c>true</c>。</returns>
    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="IsEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">是否启用嵌套平滑滚动。</param>
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="DurationProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>动画持续时间（毫秒）。</returns>
    public static int GetDuration(DependencyObject obj) => (int)obj.GetValue(DurationProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="DurationProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">动画持续时间（毫秒）。</param>
    public static void SetDuration(DependencyObject obj, int value) => obj.SetValue(DurationProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="WheelMultiplierProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>滚轮滚动倍率。</returns>
    public static double GetWheelMultiplier(DependencyObject obj) => (double)obj.GetValue(WheelMultiplierProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="WheelMultiplierProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">滚轮滚动倍率。</param>
    public static void SetWheelMultiplier(DependencyObject obj, double value) => obj.SetValue(WheelMultiplierProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="UseSmoothScrollingProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>如果使用平滑滚动则为 <c>true</c>。</returns>
    public static bool GetUseSmoothScrolling(DependencyObject obj) => (bool)obj.GetValue(UseSmoothScrollingProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="UseSmoothScrollingProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">是否使用平滑滚动。</param>
    public static void SetUseSmoothScrolling(DependencyObject obj, bool value) => obj.SetValue(UseSmoothScrollingProperty, value);

    private static NestedSmoothScrollState? GetState(DependencyObject obj) =>
        (NestedSmoothScrollState?)obj.GetValue(StateProperty);

    private static void SetState(DependencyObject obj, NestedSmoothScrollState? value) =>
        obj.SetValue(StateProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        GetState(element)?.Detach();
        SetState(element, null);

        if ((bool)e.NewValue)
        {
            var state = new NestedSmoothScrollState(element);
            SetState(element, state);
            state.Attach();
        }
    }

    private sealed class NestedSmoothScrollState
    {
        private readonly UIElement _target;
        private ScrollViewer? _scrollViewer;
        private bool _isAttached;

        public NestedSmoothScrollState(UIElement target)
        {
            _target = target;
        }

        public void Attach()
        {
            if (_isAttached)
            {
                return;
            }

            _target.PreviewMouseWheel += OnPreviewMouseWheel;
            if (_target is FrameworkElement frameworkElement)
            {
                frameworkElement.Loaded += OnLoaded;
                frameworkElement.Unloaded += OnUnloaded;
            }

            _isAttached = true;
            ResolveScrollViewer();
        }

        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            _target.PreviewMouseWheel -= OnPreviewMouseWheel;
            if (_target is FrameworkElement frameworkElement)
            {
                frameworkElement.Loaded -= OnLoaded;
                frameworkElement.Unloaded -= OnUnloaded;
            }

            if (_scrollViewer is not null)
            {
                ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
            }

            _scrollViewer = null;
            _isAttached = false;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _target.Dispatcher.BeginInvoke(ResolveScrollViewer, DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_scrollViewer is not null)
            {
                ScrollAnimationHelper.CancelVerticalAnimation(_scrollViewer);
            }
        }

        private void ResolveScrollViewer()
        {
            if (_target is Control control)
            {
                control.ApplyTemplate();
            }

            _scrollViewer = _target as ScrollViewer ?? FindDescendant<ScrollViewer>(_target);
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source
                && WheelScrollEventGuard.IsInsidePopupOrOpenComboBox(source))
            {
                return;
            }

            ResolveScrollViewer();
            if (_scrollViewer is null
                || !ModernScrollViewer.CanScrollVerticallyInWheelDirection(_scrollViewer, e.Delta))
            {
                return;
            }

            ModernScrollViewer.ScrollVerticalWheel(
                _scrollViewer,
                e,
                GetWheelMultiplier(_target),
                GetDuration(_target),
                useSmoothScrolling: GetUseSmoothScrolling(_target),
                easingFunction: null);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in EnumerateChildren(root))
        {
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<DependencyObject> EnumerateChildren(DependencyObject root)
    {
        if (root is Visual or Visual3D)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                yield return VisualTreeHelper.GetChild(root, i);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
        }

        if (root is Popup { Child: not null } popup)
        {
            yield return popup.Child;
        }
    }
}
