using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// ScorePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("DC11039A-F03C-4A26-AD70-BAB9918B7D7D",
    "ScoreControl",
    SymbolRegular.NumberRow24,
    BackendPageCategory.Internal)]
public partial class ScorePage : Page
{
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;

    public ScorePage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationScoreOpened);
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

        await runner.RunSequenceAsync(this, TutorialPageKeys.Score, TutorialOwnerLifetime.GetToken(this));
    }

    private bool IsCurrentPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }

    private void RestoreScorePreviewDefaultSort_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(ScorePreviewDataGrid.ItemsSource);
        view?.SortDescriptions.Clear();

        foreach (var column in ScorePreviewDataGrid.Columns)
        {
            column.SortDirection = null;
        }

        view?.Refresh();
    }
}
