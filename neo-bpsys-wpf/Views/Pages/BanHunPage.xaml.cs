using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// BanHunPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("19D7B197-80E5-4E79-AB3A-9AEB240088B4",
    "BanHunter",
    SymbolRegular.PresenterOff24,
    BackendPageCategory.Internal)]
public partial class BanHunPage : Page
{
    public BanHunPage()
    {
        InitializeComponent();
    }
}