using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Extensions;

namespace neo_bpsys_wpf.ExampleExtension.UI;

public partial class ExampleUI : Page
{
    public ExampleUI()
    {
        InitializeComponent();
        DataContext = ExampleExtension.Instance.ExtensionManifest;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        ExtensionManager.Instance().SharedDataService.MainTeam.Score.MinorPoints++;
    }
    
    private void ButtonBase_OnClick2(object sender, RoutedEventArgs e)
    {
        ExtensionManager.Instance().DisableExtension(ExampleExtension.Instance);
    }
}