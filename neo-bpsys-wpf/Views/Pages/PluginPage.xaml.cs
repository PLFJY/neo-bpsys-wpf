using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Views.Pages.Plugin;
using Wpf.Ui.Controls;

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
    public PluginPage()
    {
        InitializeComponent();

        PluginTabs.MenuItems.Add(new NavigationViewItem(
            "Installed",
            SymbolRegular.AppsList24,
            typeof(PluginInstalledView)));
        PluginTabs.MenuItems.Add(new NavigationViewItem(
            "PluginMarket",
            SymbolRegular.AppsAddIn24,
            typeof(PluginMarketView)));

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PluginTabs.SelectFirstItemIfNoneSelected();
    }
}
