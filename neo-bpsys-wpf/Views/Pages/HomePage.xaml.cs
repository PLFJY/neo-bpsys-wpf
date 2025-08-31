using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
public partial class HomePage : Page
{
    public HomePage(SnackbarService snackbarService)
    {
        InitializeComponent();
        snackbarService.SetSnackbarPresenter(SnbPre);
        Loaded += async (s, e) =>
        {
            await Task.Delay(1500);
            snackbarService.Show("提示", 
                "软件安装目录下有无作者名字版本的前台UI", 
                ControlAppearance.Secondary, 
                new SymbolIcon(SymbolRegular.Info24),
                TimeSpan.FromSeconds(10)
                );
        };
    }
}