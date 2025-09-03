using System.IO;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
public partial class HomePage : Page
{
    public HomePage(ISnackbarService snackbarService, ISettingsHostService settingsHostService)
    {
        InitializeComponent();
        snackbarService.SetSnackbarPresenter(SnbPre);
        if (settingsHostService.Settings.ShowTip)
            Loaded += async (s, e) =>
            {
                await Task.Delay(3200);
                snackbarService.Show("提示",
                    new HyperLinkSnackbarContent(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "bpui"),
                        "软件安装目录下有无作者名字版本的前台UI awa",
                        () =>
                        {
                            settingsHostService.Settings.ShowTip = false;
                            settingsHostService.SaveConfig();
                            snackbarService.Hide();
                        }
                        ),
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