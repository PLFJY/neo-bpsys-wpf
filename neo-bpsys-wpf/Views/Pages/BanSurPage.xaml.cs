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
/// BanSurPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("79495E85-9F05-44EE-B25A-F4866AAF1C96",
    "BanSurvivor",
    SymbolRegular.PersonProhibited24,
    BackendPageCategory.Internal)]
public partial class BanSurPage : Page
{
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BanSurPage"/> class.
    /// </summary>
    /// <param name="tutorialRunner">Tutorial runner.</param>
    /// <param name="navigationService">Navigation service.</param>
    public BanSurPage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationBanSurvivorOpened);
            if (IsCurrentBanSurPage())
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

    private bool IsCurrentBanSurPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }
}
