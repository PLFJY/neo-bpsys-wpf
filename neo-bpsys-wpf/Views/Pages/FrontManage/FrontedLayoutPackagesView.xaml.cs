using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedLayoutPackagesView.xaml 的交互逻辑
/// </summary>
public partial class FrontedLayoutPackagesView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FrontedLayoutPackagesView"/> class.
    /// </summary>
    public FrontedLayoutPackagesView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await TryRunTutorialIfCurrentAsync();
        IsVisibleChanged += async (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                await TryRunTutorialIfCurrentAsync();
            }
        };
    }

    private async Task TryRunTutorialIfCurrentAsync()
    {
        if (!IsCurrentFrontManageChild())
        {
            return;
        }

        var runner = IAppHost.Host?.Services.GetService<ITutorialRunner>();
        if (runner == null)
        {
            return;
        }

        await runner.TryRunNextPackageAsync(this, TutorialPageKey);
    }

    private bool IsCurrentFrontManageChild()
    {
        var navigationService = IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService?.CurrentPageContent is FrontManagePage frontManagePage
            && FrontManagePage.TryResolveCurrentChildTutorial(
                frontManagePage.FrontManageTabs,
                out var owner,
                out var pageKey)
            && ReferenceEquals(owner, this)
            && string.Equals(pageKey, TutorialPageKey, StringComparison.Ordinal);
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
