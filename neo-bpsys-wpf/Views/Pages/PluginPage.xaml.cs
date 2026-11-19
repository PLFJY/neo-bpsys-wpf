using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Views.Pages.Plugin;
using Wpf.Ui.Controls;
using neo_bpsys_wpf.Controls.Modern.Navigation;
using neo_bpsys_wpf.ViewModels.Pages;
using System.ComponentModel;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// PluginPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("94ABE666-EB81-4244-BDA2-A5E6486FB091",
    "Plugins",
    SymbolRegular.AppsAddIn24,
    BackendPageCategory.External)]
public partial class PluginPage : Page
{
    private readonly ModernNavigationEntry _marketEntry;
    private PluginPageViewModel? _viewModel;

    /// <summary>创建插件管理页与市场导航入口。</summary>
    public PluginPage()
    {
        InitializeComponent();

        PluginTabs.MenuItems.Add(new NavigationViewItem(
            "Installed",
            SymbolRegular.AppsList24,
            typeof(PluginInstalledView)));
        _marketEntry = ModernNavigationEntry.FromSource(new NavigationViewItem(
            "PluginMarket",
            SymbolRegular.AppsAddIn24,
            typeof(PluginMarketView)), false);
        PluginTabs.MenuItems.Add(_marketEntry);

        Loaded += OnLoaded;
        Unloaded += (_, _) => Observe(null);
        DataContextChanged += (_, _) => { if (IsLoaded) Observe(DataContext as PluginPageViewModel); };
        Helpers.BackendPresentation.Attach(this, () => (DataContext as PluginPageViewModel)?.EnsureMarketLoaded());
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Observe(DataContext as PluginPageViewModel);
        PluginTabs.SelectFirstItemIfNoneSelected();
    }

    private void Observe(PluginPageViewModel? viewModel)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = viewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
        _marketEntry.HasNotification = _viewModel?.HasInstalledPluginUpdates == true;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PluginPageViewModel.HasInstalledPluginUpdates))
            _marketEntry.HasNotification = _viewModel?.HasInstalledPluginUpdates == true;
        if (e.PropertyName == nameof(PluginPageViewModel.SelectedMarketPlugin) && _viewModel?.SelectedMarketPlugin is not null)
            PluginTabs.Navigate(typeof(PluginMarketView));
    }
}
