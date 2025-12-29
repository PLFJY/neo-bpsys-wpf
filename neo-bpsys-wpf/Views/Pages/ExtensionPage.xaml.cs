using System.Windows.Controls;
using System.Windows;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// ExtensionPage.xaml 的交互逻辑
/// </summary>
public partial class ExtensionPage : Page
{
    public ExtensionPage()
    {
        InitializeComponent();
        Loaded += ExtensionPage_Loaded;
    }

    private async void ExtensionPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ExtensionPage_Loaded;

        if (DataContext is ExtensionPageViewModel vm)
        {
            await vm.RefreshAsync();
        }
    }

    private void Border_ManipulationInertiaStarting(object sender, System.Windows.Input.ManipulationInertiaStartingEventArgs e)
    {

    }
}