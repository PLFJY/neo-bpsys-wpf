using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// TalentPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("231F566E-77F6-43D0-BD1A-CFDA81F45CE2",
    "TalentAndTrait",
    SymbolRegular.PersonWalking24,
    BackendPageCategory.Internal)]
public partial class TalentPage : Page
{
    public TalentPage()
    {
        InitializeComponent();
    }
}