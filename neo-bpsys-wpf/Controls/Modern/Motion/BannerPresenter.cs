using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Motion;

/// <summary>为浮层横幅提供可反向的顶部揭示动画，退场完成后才折叠。</summary>
public sealed class BannerPresenter : Decorator
{
    /// <summary>标识横幅是否打开的依赖属性。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(BannerPresenter), new PropertyMetadata(false, OnIsOpenChanged));
    private static readonly DependencyProperty RevealProperty = DependencyProperty.Register(
        "Reveal", typeof(double), typeof(BannerPresenter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
    private readonly RectangleGeometry _clip = new();
    private readonly TranslateTransform _translation = new();
    private double _naturalHeight;
    private int _version;

    /// <summary>初始化横幅呈现器。</summary>
    public BannerPresenter()
    {
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        Loaded += (_, _) =>
        {
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
            TransitionTo(IsOpen);
        };
        Unloaded += (_, _) =>
        {
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
            StopAnimations();
            SetCurrentValue(RevealProperty, 0d);
            SetCurrentValue(OpacityProperty, 0d);
            Visibility = Visibility.Collapsed;
            IsHitTestVisible = false;
        };
    }

    /// <summary>获取或设置横幅是否打开；关闭立即停止接收输入。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    private double Reveal => Math.Clamp((double)GetValue(RevealProperty), 0, 1);

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (BannerPresenter)sender;
        if (presenter.IsLoaded) presenter.TransitionTo((bool)e.NewValue);
        else if (e.NewValue is true) presenter.Visibility = Visibility.Visible;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return default;
        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        _naturalHeight = Child.DesiredSize.Height;
        return new Size(Child.DesiredSize.Width, _naturalHeight * Reveal);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is not null)
        {
            Child.RenderTransform = _translation;
            Child.Arrange(new Rect(0, 0, arrangeSize.Width, _naturalHeight));
            _translation.Y = -_naturalHeight * (1 - Reveal);
        }
        Clip = Reveal >= 1 ? null : _clip;
        _clip.Rect = new Rect(0, 0, arrangeSize.Width, _naturalHeight * Reveal);
        return arrangeSize;
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        if (Dispatcher.CheckAccess()) { if (IsLoaded) TransitionTo(IsOpen); }
        else _ = Dispatcher.BeginInvoke(() => { if (IsLoaded) TransitionTo(IsOpen); });
    }

    private void StopAnimations()
    {
        _version++;
        BeginAnimation(RevealProperty, null);
        BeginAnimation(OpacityProperty, null);
    }

    private void TransitionTo(bool open)
    {
        var from = Reveal;
        var opacity = Opacity;
        StopAnimations();
        var version = _version;
        var target = open ? 1d : 0d;
        IsHitTestVisible = open;
        SetCurrentValue(RevealProperty, target);
        SetCurrentValue(OpacityProperty, target);
        if (!SystemParameters.ClientAreaAnimation || Math.Abs(from - target) < 0.001)
        {
            Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        // ContextMenuMgr 使用的 Fluent 控件入场/退场曲线与时间。
        var duration = TimeSpan.FromMilliseconds(open ? 250 : 167);
        var motion = new DoubleAnimationUsingKeyFrames { Duration = duration, FillBehavior = FillBehavior.Stop };
        motion.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, TimeSpan.Zero));
        motion.KeyFrames.Add(new SplineDoubleKeyFrame(target, duration,
            open ? new KeySpline(0, 0, 0, 1) : new KeySpline(1, 0, 1, 1)));
        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration, FillBehavior = FillBehavior.Stop };
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(opacity, TimeSpan.Zero));
        if (!open) fade.KeyFrames.Add(new LinearDoubleKeyFrame(opacity, duration - TimeSpan.FromMilliseconds(83)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(target, open ? TimeSpan.FromMilliseconds(83) : duration));
        motion.Completed += (_, _) =>
        {
            if (version != _version || !IsLoaded) return;
            StopAnimations();
            Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        };
        BeginAnimation(RevealProperty, motion, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }
}
