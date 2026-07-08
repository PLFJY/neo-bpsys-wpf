using System.Windows.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedWindowsView.xaml 的交互逻辑
/// </summary>
public partial class FrontedWindowsView : UserControl
{
    public FrontedWindowsView()
    {
        InitializeComponent();
        Loaded += (_, _) => RunSelfTutorialIfFrontManageOverviewCompleted("Loaded");
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                RunSelfTutorialIfFrontManageOverviewCompleted("Visible");
            }
        };
    }

    private void RunSelfTutorialIfFrontManageOverviewCompleted(string reason)
    {
        if (IAppHost.Host is null
            || !TutorialDefinitionHelpers.IsPackageRecorded(
                IAppHost.Host.Services,
                TutorialPackageIds.FrontManageOverview))
        {
            return;
        }

        TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKey, reason);
    }
}
