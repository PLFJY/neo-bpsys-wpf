using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// TeamInfoPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("54B0068C-7DF1-408A-997C-B16F6E099471",
    "TeamInfo",
    SymbolRegular.PeopleTeam24,
    BackendPageCategory.Internal)]
public partial class TeamInfoPage : Page
{
    public TeamInfoPage()
    {
        InitializeComponent();
    }
}