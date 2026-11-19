using neo_bpsys_wpf.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using neo_bpsys_wpf.Controls.Modern.Scrolling;

namespace neo_bpsys_wpf.Controls;

/// <summary>MainWindow 内的公告层。</summary>
public partial class AnnouncementOverlay : UserControl
{
    private AnnouncementCenterViewModel? _viewModel;
    private int _animationVersion;

    /// <summary>创建公告层。</summary>
    public AnnouncementOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            ObserveViewModel();
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        };
        Unloaded += (_, _) =>
        {
            if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
            StopAnimations();
        };
        ApplyState(false);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded) ObserveViewModel();
    }

    private void ObserveViewModel()
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as AnnouncementCenterViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnnouncementCenterViewModel.IsOpen)) UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => { if (IsLoaded) UpdateVisibility(); });
            return;
        }
        var open = _viewModel?.IsOpen == true;
        if (!IsLoaded || !SystemParameters.ClientAreaAnimation)
        {
            StopAnimations();
            ApplyState(open);
            return;
        }
        if (!open && Visibility != Visibility.Visible) return;
        var group = (TransformGroup)AnnouncementCard.RenderTransform;
        var scale = (ScaleTransform)group.Children[0];
        var translation = (TranslateTransform)group.Children[1];
        var opacity = BackdropScrim.Opacity;
        var size = scale.ScaleX;
        var y = translation.Y;
        StopAnimations();
        var version = _animationVersion;
        ApplyState(open);
        Visibility = Visibility.Visible;
        var duration = TimeSpan.FromMilliseconds(open ? 250 : 167);
        DoubleAnimation Animation(double from, double to) => new(from, to, duration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn }
        };
        var motion = Animation(y, open ? 0 : 10);
        motion.Completed += (_, _) =>
        {
            if (version != _animationVersion || !IsLoaded) return;
            StopAnimations();
            ApplyState(open);
        };
        BackdropScrim.BeginAnimation(OpacityProperty, Animation(opacity, open ? 1 : 0));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(size, open ? 1 : 0.98));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(size, open ? 1 : 0.98));
        translation.BeginAnimation(TranslateTransform.YProperty, motion);
    }

    private void ApplyState(bool open)
    {
        Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        IsHitTestVisible = open;
        BackdropScrim.Opacity = open ? 1 : 0;
        var transforms = (TransformGroup)AnnouncementCard.RenderTransform;
        ((ScaleTransform)transforms.Children[0]).ScaleX = open ? 1 : 0.98;
        ((ScaleTransform)transforms.Children[0]).ScaleY = open ? 1 : 0.98;
        ((TranslateTransform)transforms.Children[1]).Y = open ? 0 : 10;
    }

    private void StopAnimations()
    {
        _animationVersion++;
        BackdropScrim.BeginAnimation(OpacityProperty, null);
        var transforms = (TransformGroup)AnnouncementCard.RenderTransform;
        transforms.Children[0].BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transforms.Children[0].BeginAnimation(ScaleTransform.ScaleYProperty, null);
        transforms.Children[1].BeginAnimation(TranslateTransform.YProperty, null);
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) UpdateVisibility();
    }

    private void MarkdownViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var target = ScrollAnimationHelper.GetCurrentVerticalAnimationTarget(AnnouncementsScrollViewer)
            ?? AnnouncementsScrollViewer.VerticalOffset;
        ScrollAnimationHelper.SmoothScrollToVerticalOffset(AnnouncementsScrollViewer, target - e.Delta / 3.0);
    }
}
