using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// MapBpPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("AFC99F3A-8845-4971-82FA-3E064E238A29",
    "MapBP",
    SymbolRegular.Map24,
    BackendPageCategory.Internal)]
public partial class MapBpPage : Page
{
    public MapBpPage()
    {
        InitializeComponent();
    }
}