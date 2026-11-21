using System.Windows;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Motion;

/// <summary>为异步内容和反馈状态提供短暂淡入，卸载或替换时释放动画。</summary>
public static class ContentEntrance
{
    /// <summary>标识是否启用内容淡入的附加属性。</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ContentEntrance), new PropertyMetadata(false, OnEnabledChanged));
    private static readonly DependencyProperty AnimationProperty = DependencyProperty.RegisterAttached(
        "Animation", typeof(DoubleAnimation), typeof(ContentEntrance));

    /// <summary>获取是否启用内容淡入。</summary>
    /// <param name="owner">目标元素。</param>
    /// <returns>是否启用淡入。</returns>
    public static bool GetIsEnabled(DependencyObject owner) => (bool)owner.GetValue(IsEnabledProperty);

    /// <summary>设置是否启用内容淡入。</summary>
    /// <param name="owner">目标元素。</param>
    /// <param name="value">是否启用淡入。</param>
    public static void SetIsEnabled(DependencyObject owner, bool value) => owner.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is not FrameworkElement element) return;
        element.Loaded -= OnLoaded;
        element.IsVisibleChanged -= OnVisibleChanged;
        element.Unloaded -= OnUnloaded;
        Stop(element);
        if (e.NewValue is not true) return;
        element.Loaded += OnLoaded;
        element.IsVisibleChanged += OnVisibleChanged;
        element.Unloaded += OnUnloaded;
        if (element.IsLoaded) Animate(element);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Animate((FrameworkElement)sender);
    private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) Animate((FrameworkElement)sender);
        else Stop((FrameworkElement)sender);
    }
    private static void OnUnloaded(object sender, RoutedEventArgs e) => Stop((FrameworkElement)sender);

    private static void Stop(FrameworkElement element)
    {
        if (element.GetValue(AnimationProperty) is not DoubleAnimation) return;
        element.ClearValue(AnimationProperty);
        element.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private static void Animate(FrameworkElement element)
    {
        Stop(element);
        if (!element.IsLoaded || !element.IsVisible || !SystemParameters.ClientAreaAnimation) return;
        var animation = new DoubleAnimation(0, element.Opacity, TimeSpan.FromMilliseconds(83))
        { FillBehavior = FillBehavior.Stop };
        element.SetValue(AnimationProperty, animation);
        animation.Completed += (_, _) =>
        {
            if (ReferenceEquals(element.GetValue(AnimationProperty), animation)) Stop(element);
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
