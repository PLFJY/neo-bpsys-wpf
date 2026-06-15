using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// FrontManagePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("0532C747-A9E8-44E7-A8FB-7416DF8FC4C6",
    "FrontendManagement",
    SymbolRegular.ShareScreenStart24,
    BackendPageCategory.External)]
public partial class FrontManagePage : Page, IRecipient<FrontManageTabNavigationMessage>
{
    public FrontManagePage()
    {
        InitializeComponent();

        FrontManageTabs.MenuItems.Add(new NavigationViewItem(
            "FrontendWindows",
            SymbolRegular.ShareScreenStart24,
            typeof(FrontedWindowsView)));
        FrontManageTabs.MenuItems.Add(new NavigationViewItem(
            "LayoutPackages",
            SymbolRegular.AppsList24,
            typeof(FrontedLayoutPackagesView)));

        Loaded += OnLoaded;
        WeakReferenceMessenger.Default.Register(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FrontManageTabs.SelectFirstItemIfNoneSelected();
    }

    /// <inheritdoc/>
    public void Receive(FrontManageTabNavigationMessage message)
    {
        if (message.TabKey == FrontManageTabNavigationMessage.LayoutPackagesTabKey)
        {
            FrontManageTabs.Navigate(typeof(FrontedLayoutPackagesView));
        }
    }
}
