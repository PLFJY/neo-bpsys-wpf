using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// BanHunPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("19D7B197-80E5-4E79-AB3A-9AEB240088B4",
    "BanHunter",
    SymbolRegular.PresenterOff24,
    BackendPageCategory.Internal)]
public partial class BanHunPage : Page
{
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;

    public BanHunPage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationBanHunterOpened);
            if (IsCurrentPage())
            {
                await TryRunTutorialAsync();
            }
        };
    }

    internal async Task TryRunTutorialAsync()
    {
        var runner = _tutorialRunner ?? IAppHost.Host?.Services.GetService<ITutorialRunner>();
        if (runner == null)
        {
            return;
        }

        await runner.TryRunNextPackageAsync(this, TutorialPageKey);
    }

    private bool IsCurrentPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }
}
