using neo_bpsys_wpf.Core.Attributes;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.TeamJsonMaker;

/// <summary>
/// TeamJsonMaker.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("DAC4110D-FCCA-431A-8842-45FA7146D303",
    "队伍 JSON 信息制作",
    SymbolRegular.DocumentEdit24)]
public partial class TeamJsonMaker : Page
{
    public TeamJsonMaker()
    {
        InitializeComponent();
    }
}