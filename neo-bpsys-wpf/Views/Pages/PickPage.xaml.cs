using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ProductTour;
using Wpf.Ui.Controls;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Core.Abstractions.Services;

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
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PickPage"/> class.
    /// </summary>
    /// <param name="tutorialRunner">Tutorial runner.</param>
    /// <param name="navigationService">Navigation service.</param>
    public PickPage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationPickCharacterOpened);
            if (IsCurrentPickPage())
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

    private bool IsCurrentPickPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }
}
