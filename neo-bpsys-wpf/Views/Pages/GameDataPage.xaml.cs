using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// GameDataPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("44A64936-3D1C-42B4-8BD1-4144455F37E6",
    "GameData",
    SymbolRegular.TextNumberListLtr24,
    BackendPageCategory.Internal)]
public partial class GameDataPage : Page
{
    public GameDataPage()
    {
        InitializeComponent();
    }
}