using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
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
    }
}