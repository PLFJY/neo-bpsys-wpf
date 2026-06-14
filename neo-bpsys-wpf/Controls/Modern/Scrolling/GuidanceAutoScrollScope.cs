using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 提供引导自动滚动的附加属性，监听引导高亮消息并自动滚动到目标元素。
/// </summary>
public static class GuidanceAutoScrollScope
{
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(GuidanceAutoScrollState),
            typeof(GuidanceAutoScrollScope),
            new PropertyMetadata(null));

    /// <summary>
    /// <see cref="IsEnabledProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(GuidanceAutoScrollScope),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// 获取指定元素的 <see cref="IsEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>如果启用了引导自动滚动则为 <c>true</c>。</returns>
    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="IsEnabledProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">是否启用引导自动滚动。</param>
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static GuidanceAutoScrollState? GetState(DependencyObject obj) =>
        (GuidanceAutoScrollState?)obj.GetValue(StateProperty);

    private static void SetState(DependencyObject obj, GuidanceAutoScrollState? value) =>
        obj.SetValue(StateProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        GetState(element)?.Detach();
        SetState(element, null);

        if ((bool)e.NewValue)
        {
            var state = new GuidanceAutoScrollState(element);
            SetState(element, state);
            state.Attach();
        }
    }

    private sealed class GuidanceAutoScrollState
    {
        private readonly FrameworkElement _scope;
        private bool _isRegistered;
        private bool _isLifecycleAttached;

        public GuidanceAutoScrollState(FrameworkElement scope)
        {
            _scope = scope;
        }

        public void Attach()
        {
            if (!_isLifecycleAttached)
            {
                _scope.Loaded += OnLoaded;
                _scope.Unloaded += OnUnloaded;
                _isLifecycleAttached = true;
            }

            if (_scope.IsLoaded)
            {
                Register();
            }
        }

        public void Detach()
        {
            Unregister();

            if (_isLifecycleAttached)
            {
                _scope.Loaded -= OnLoaded;
                _scope.Unloaded -= OnUnloaded;
                _isLifecycleAttached = false;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Register();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unregister();
        }

        private void Register()
        {
            if (_isRegistered)
            {
                return;
            }

            WeakReferenceMessenger.Default.Register<HighlightMessage>(_scope, OnHighlightMessage);
            _isRegistered = true;
        }

        private void Unregister()
        {
            if (!_isRegistered)
            {
                return;
            }

            WeakReferenceMessenger.Default.Unregister<HighlightMessage>(_scope);
            _isRegistered = false;
        }

        private void OnHighlightMessage(object recipient, HighlightMessage message)
        {
            if (message.GameAction is null || !_scope.IsLoaded)
            {
                return;
            }

            _scope.Dispatcher.BeginInvoke(
                () => ScrollToMessageTarget(message, allowRetry: true),
                DispatcherPriority.ContextIdle);
        }

        private void ScrollToMessageTarget(HighlightMessage message, bool allowRetry)
        {
            if (!_scope.IsLoaded)
            {
                return;
            }

            var target = GuidanceScrollHelper.FindBestTarget(_scope, message);
            if (target is not null)
            {
                GuidanceScrollHelper.ScrollElementIntoView(target);
                return;
            }

            if (!allowRetry)
            {
                return;
            }

            _scope.Dispatcher.BeginInvoke(
                () => ScrollToMessageTarget(message, allowRetry: false),
                DispatcherPriority.ApplicationIdle);
        }
    }
}
