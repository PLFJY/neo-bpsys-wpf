using neo_bpsys_wpf.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls;

/// <summary>MainWindow 内的公告层。</summary>
public partial class AnnouncementOverlay : UserControl
{
    private AnnouncementCenterViewModel? _viewModel;

    /// <summary>创建公告层。</summary>
    public AnnouncementOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = e.NewValue as AnnouncementCenterViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnnouncementCenterViewModel.IsOpen)) UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_viewModel?.IsOpen == true)
        {
            Visibility = Visibility.Visible;
            IsHitTestVisible = true;
            ((Storyboard)Resources["AnnouncementCloseAnimation"]).Stop(this);
            ((Storyboard)Resources["AnnouncementOpenAnimation"]).Begin(this, true);
        }
        else if (Visibility == Visibility.Visible)
        {
            IsHitTestVisible = false;
            var close = ((Storyboard)Resources["AnnouncementCloseAnimation"]).Clone();
            close.Completed += (_, _) =>
            {
                if (_viewModel?.IsOpen != true) Visibility = Visibility.Collapsed;
            };
            close.Begin(this, true);
        }
    }

    private void MarkdownViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        AnnouncementsScrollViewer.ScrollToVerticalOffset(
            AnnouncementsScrollViewer.VerticalOffset - e.Delta / 3.0);
    }
}
