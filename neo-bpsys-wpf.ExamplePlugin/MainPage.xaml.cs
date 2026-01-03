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

namespace neo_bpsys_wpf.ExamplePlugin;

/// <summary>
/// MainPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("A232B1A8-C31F-46F0-B8D0-F3ED0CF8C6DC",
    "示例插件",
    SymbolRegular.PlugConnected24)]
public partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }
}