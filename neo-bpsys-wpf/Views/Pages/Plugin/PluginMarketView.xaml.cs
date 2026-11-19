using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Frame;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Views.Pages.Plugin;

/// <summary>在单一内容宿主中呈现市场目录、详情、下载和设置。</summary>
public partial class PluginMarketView : UserControl
{
    private PluginPageViewModel? _viewModel;
    private PluginMarketBrowseView? _browse;
    private PluginMarketDetailsView? _details;
    private PluginMarketDownloadsView? _downloads;
    private PluginMarketSettingsView? _settings;
    private DispatcherOperation? _pendingNavigation;
    private bool _focusAfterNavigation;

    /// <summary>创建市场视图，保留原有命令和页面加载契约。</summary>
    public PluginMarketView()
    {
        InitializeComponent();
        Loaded += (_, _) => ObserveViewModel();
        Unloaded += (_, _) =>
        {
            _pendingNavigation?.Abort();
            _pendingNavigation = null;
            if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = null;
            _details?.Present(null);
        };
        DataContextChanged += (_, _) => { if (IsLoaded) ObserveViewModel(); };
        MarketFrame.NavigationCompleted += (_, _) =>
        {
            if (!ReferenceEquals(MarketFrame.CurrentContent, _details)) _details?.Present(null);
            if (_focusAfterNavigation)
            {
                _focusAfterNavigation = false;
                if (_viewModel?.HasPluginMarketOverlay == true) BackButton.Focus();
                else _browse?.FocusCatalog();
            }
        };
        Helpers.BackendPresentation.Attach(this, () => _viewModel?.EnsureMarketLoaded());
    }

    private void ObserveViewModel()
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = DataContext as PluginPageViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
        QueueNavigation();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PluginPageViewModel.SelectedMarketPlugin)
            or nameof(PluginPageViewModel.IsDownloadQueueOpen) or nameof(PluginPageViewModel.IsPluginMarketSettingsOpen))
            QueueNavigation();
    }

    private void QueueNavigation()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => { if (IsLoaded) QueueNavigation(); });
            return;
        }
        if (_pendingNavigation is { Status: DispatcherOperationStatus.Pending }) return;
        _pendingNavigation = Dispatcher.BeginInvoke(() =>
        {
            _pendingNavigation = null;
            if (!IsLoaded || _viewModel is null) return;
            FrameworkElement target;
            if (_viewModel.IsDownloadQueueOpen) target = _downloads ??= new PluginMarketDownloadsView();
            else if (_viewModel.IsPluginMarketSettingsOpen) target = _settings ??= new PluginMarketSettingsView();
            else if (_viewModel.SelectedMarketPlugin is { } plugin)
            {
                _details ??= new PluginMarketDetailsView();
                _details.Present(plugin);
                target = _details;
            }
            else target = _browse ??= new PluginMarketBrowseView();
            if (ReferenceEquals(target, MarketFrame.CurrentContent)) return;
            _focusAfterNavigation = MarketFrame.CurrentContent is not null;
            // 退出方向与进入方向对应；每个子视图自己拥有滚动，不进入主导航返回栈。
            MarketFrame.Navigate(target, new SlideNavigationTransitionInfo
            {
                Effect = ReferenceEquals(target, _browse)
                    ? SlideNavigationTransitionEffect.FromLeft : SlideNavigationTransitionEffect.FromRight
            });
            MarketFrame.ClearJournal();
        }, DispatcherPriority.Loaded);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.HasPluginMarketOverlay != true) return;
        if (e.Key == Key.Escape && (e.OriginalSource is ComboBox { IsDropDownOpen: true }
            || e.OriginalSource is ComboBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is ComboBox { IsDropDownOpen: true })) return;
        if (e.Key != Key.Escape && !(e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)) return;
        _viewModel.ClosePluginMarketOverlaysCommand.Execute(null);
        e.Handled = true;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var visibility = ActualWidth < 600 ? Visibility.Collapsed : Visibility.Visible;
        BackLabel.Visibility = visibility;
        DownloadsLabel.Visibility = visibility;
        SettingsLabel.Visibility = visibility;
    }
}
