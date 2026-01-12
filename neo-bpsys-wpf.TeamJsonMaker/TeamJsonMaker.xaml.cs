using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
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