using neo_bpsys_wpf.Core.Attributes;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ExamplePlugin.Views;

/// <summary>
/// MainPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("A232B1A8-C31F-46F0-B8D0-F3ED0CF8C6DC",
    "示例插件",
    SymbolRegular.PlugConnected24)]
public partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }
}