using System.IO;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Services;
using Wpf.Ui.Controls;

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
            await Task.Delay(3200);
            snackbarService.Show("提示",
                new HyperLinkSnackbarContent()
                {
                    NavigateUri = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "bpui"),
                    Text = "软件安装目录下有无作者名字版本的前台UI awa"
                },
                ControlAppearance.Secondary,
                new SymbolIcon(SymbolRegular.Info24, 24D)
                {
                    Margin = new Thickness(0, 0, 5, 0)
                },
                TimeSpan.FromSeconds(10),
                false
                );
        };
    }
}