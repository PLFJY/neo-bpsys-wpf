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
                new HyperLinkSnackbarMessage()
                {
                    HyperLinkUri = new Uri(Path.Combine(Environment.CurrentDirectory, "Resources")).ToString(),
                }, 
                ControlAppearance.Secondary, 
                new SymbolIcon(SymbolRegular.Info24, 24D)
                {
                    Margin = new Thickness(0,0,5,0)
                },
                TimeSpan.FromSeconds(10),
                false
                );
        };
    }
}