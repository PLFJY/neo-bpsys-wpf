using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 主题切换开始时通知页面级过渡动画。
/// 主窗口的主题遮罩动画只覆盖其自身内容层，独立页面/子视图需要单独参与淡入淡出。
/// </summary>
internal static class ThemeTransitionBus
{
    /// <summary>
    /// 主题切换开始事件。
    /// </summary>
    public static event EventHandler? Started;

    /// <summary>
    /// 通知所有订阅者主题切换开始。
    /// </summary>
    public static void NotifyStarted()
    {
        Started?.Invoke(null, EventArgs.Empty);
    }
}

/// <summary>
/// 让页面或子视图在主题切换时执行一次轻微的透明度过渡。
/// </summary>
internal static class ThemeTransitionBehavior
{
    private static readonly ConditionalWeakTable<FrameworkElement, EventHandler> Handlers = new();

    /// <summary>
    /// 标识是否启用主题切换过渡的附加属性。
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ThemeTransitionBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// 设置是否启用主题切换过渡。
    /// </summary>
    /// <param name="element">目标元素。</param>
    /// <param name="value">是否启用。</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// 获取是否启用主题切换过渡。
    /// </summary>
    /// <param name="element">目标元素。</param>
    /// <returns>是否启用。</returns>
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (e.NewValue is true)
        {
            element.Loaded += Element_OnLoaded;
            element.Unloaded += Element_OnUnloaded;
            if (element.IsLoaded)
            {
                Attach(element);
            }
        }
        else
        {
            Detach(element);
            element.Loaded -= Element_OnLoaded;
            element.Unloaded -= Element_OnUnloaded;
        }
    }

    private static void Element_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Attach(element);
        }
    }

    private static void Element_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Detach(element);
        }
    }

    private static void Attach(FrameworkElement element)
    {
        if (Handlers.TryGetValue(element, out _))
        {
            return;
        }

        void Handler(object? sender, EventArgs args) => Animate(element);
        Handlers.Add(element, Handler);
        ThemeTransitionBus.Started += Handler;
    }

    private static void Detach(FrameworkElement element)
    {
        if (!Handlers.TryGetValue(element, out var handler))
        {
            return;
        }

        ThemeTransitionBus.Started -= handler;
        Handlers.Remove(element);
    }

    private static void Animate(FrameworkElement element)
    {
        if (!element.IsVisible)
        {
            return;
        }

        // 页面级内容在主题切换时轻微淡出再恢复，使不透明卡片/列表也参与过渡。
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(500)
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1D, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.72D, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1D, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
        element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
