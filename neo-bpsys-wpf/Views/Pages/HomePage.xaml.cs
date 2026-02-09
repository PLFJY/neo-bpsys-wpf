using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("76538B42-BA3A-4A86-B696-902D3AF9E777",
    "HomePage",
    SymbolRegular.Home24,
    BackendPageCategory.Internal)]
public partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
    }
}