using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedLayoutPackagesView.xaml 的交互逻辑
/// </summary>
public partial class FrontedLayoutPackagesView : UserControl
{
    public FrontedLayoutPackagesView()
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

    private void PackageListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FrontManagePageViewModel viewModel
            && viewModel.ActivateSelectedPackageByDoubleClickCommand.CanExecute(null))
        {
            viewModel.ActivateSelectedPackageByDoubleClickCommand.Execute(null);
        }
    }

    private void PackageListBox_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (sender == LayoutPackageList && e.OriginalSource is ListBoxItem)
        {
            e.Handled = true;
        }
    }
}
