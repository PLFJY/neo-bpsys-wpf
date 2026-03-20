using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// PickPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("0E1DC561-EAE8-4455-981D-BB84003A2AAC",
    "PickCharacter",
    SymbolRegular.PersonAdd24,
    BackendPageCategory.Internal)]
public partial class PickPage : Page
{
    public PickPage()
    {
        InitializeComponent();
    }
}