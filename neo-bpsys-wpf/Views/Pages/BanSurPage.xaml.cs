using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// BanSurPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("79495E85-9F05-44EE-B25A-F4866AAF1C96",
    "BanSurvivor",
    SymbolRegular.PersonProhibited24,
    BackendPageCategory.Internal)]
public partial class BanSurPage : Page
{
    public BanSurPage()
    {
        InitializeComponent();
    }
}