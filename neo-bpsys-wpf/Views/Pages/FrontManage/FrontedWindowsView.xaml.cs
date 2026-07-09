using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedWindowsView.xaml 的交互逻辑
/// </summary>
public partial class FrontedWindowsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FrontedWindowsView"/> class.
    /// </summary>
    public FrontedWindowsView()
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
}
