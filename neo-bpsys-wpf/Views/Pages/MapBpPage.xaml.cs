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
/// MapBpPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("AFC99F3A-8845-4971-82FA-3E064E238A29",
    "MapBP",
    SymbolRegular.Map24,
    BackendPageCategory.Internal)]
public partial class MapBpPage : Page
{
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;

    public MapBpPage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationMapBpOpened);
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

        await runner.TryRunNextPackageAsync(this, TutorialPageKeys.BpGameGuidance);
    }

    private bool IsCurrentPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }
}
