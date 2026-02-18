using neo_bpsys_wpf.Core.Attributes;
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

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBpPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("6E5AB941-A4A0-4D43-B9CB-381364414C1B",
    "SmartBpPage",
    Wpf.Ui.Controls.SymbolRegular.ScanText24,
    Core.Enums.BackendPageCategory.External)]
public partial class SmartBpPage : Page
{
    public SmartBpPage()
    {
        InitializeComponent();
    }
}
