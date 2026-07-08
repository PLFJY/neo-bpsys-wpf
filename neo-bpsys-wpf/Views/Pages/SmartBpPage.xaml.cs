using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Tutorial;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBpPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("6E5AB941-A4A0-4D43-B9CB-381364414C1B",
    "SmartBp",
    Wpf.Ui.Controls.SymbolRegular.ScanText24,
    Core.Enums.BackendPageCategory.External)]
public partial class SmartBpPage : Page
{
    public SmartBpPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationSmartBpOpened);
            TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.SmartBp);
        };
    }
}
