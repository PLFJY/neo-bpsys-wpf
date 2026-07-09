using System.Windows;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers the standard first-run BP tutorial flow.
/// </summary>
public static class FirstRunStandardBpTour
{
    /// <summary>The standard first-run BP tutorial flow id.</summary>
    public const string FlowId = TutorialFlowIds.FirstRunStandardBp;

    /// <summary>Package ids covered by the first-run BP tutorial flow.</summary>
    public static readonly string[] IncludedPackages =
    [
        MainWindow.TutorialPackages.NavigationFrontManage,
        FrontedWindowsView.TutorialPackages.BpWindowLaunchBasic,
        MainWindow.TutorialPackages.NavigationTeamInfo,
        TeamInfoPage.TutorialPackages.TeamNameBasic,
        MainWindow.TutorialPackages.TeamSummaryBasic,
        TeamInfoPage.TutorialPackages.JsonImportPreset,
        TeamInfoPage.TutorialPackages.PlayerManage,
        MainWindow.TutorialPackages.GameProgressBo1FirstHalf,
        TutorialPackageIds.BpGameGuidanceStartBasic,
        TutorialPackageIds.BpGameGuidanceCurrentStepBasic,
        TutorialPackageIds.MapBpBanMapOperationBasic,
        TutorialPackageIds.MapBpNextToPickMapBasic,
        BanSurPage.TutorialPackages.CharacterSelectorBasic,
        PickPage.TutorialPackages.SelectFourSurvivorsBasic,
        PickPage.TutorialPackages.GlobalBanRecordBasic,
        PickPage.TutorialPackages.CharacterChangerBasic,
        TalentPage.TutorialPackages.Basic,
        TutorialPackageIds.BpGameGuidanceEndBasic,
        MainWindow.TutorialPackages.NavigationScore,
        ScorePage.TutorialPackages.Basic,
        MainWindow.TutorialPackages.NewGameBasic,
        MainWindow.TutorialPackages.GlobalBanCarryOver
    ];

    /// <summary>
    /// Registers the standard first-run BP tutorial flow.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        var builder = TutorialFlowBuilder.Create(FlowId)
            .Version(1);

        foreach (var packageId in IncludedPackages)
        {
            builder.Include(packageId);
        }

        builder
            .Dialogue(string.Empty, NeoBpsysTutorialTexts.FirstRunOpeningDialogue())
            .Package(MainWindow.TutorialPackages.NavigationFrontManage)
            .Package(FrontedWindowsView.TutorialPackages.BpWindowLaunchBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(MainWindow.TutorialPackages.NavigationTeamInfo)
            .Package(TeamInfoPage.TutorialPackages.TeamNameBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(MainWindow.TutorialPackages.TeamSummaryBasic)
            .Package(TeamInfoPage.TutorialPackages.JsonImportPreset)
            .Package(TeamInfoPage.TutorialPackages.PlayerManage)
            .Item(CreateMainWindowActivateAction())
            .Package(MainWindow.TutorialPackages.GameProgressBo1FirstHalf)
            .Package(TutorialPackageIds.BpGameGuidanceStartBasic)
            .Package(TutorialPackageIds.BpGameGuidanceCurrentStepBasic)
            .Package(TutorialPackageIds.MapBpBanMapOperationBasic)
            .Package(TutorialPackageIds.MapBpNextToPickMapBasic)
            .Package(BanSurPage.TutorialPackages.CharacterSelectorBasic)
            .Package(PickPage.TutorialPackages.SelectFourSurvivorsBasic)
            .Package(PickPage.TutorialPackages.GlobalBanRecordBasic)
            .Package(PickPage.TutorialPackages.CharacterChangerBasic)
            .Package(TalentPage.TutorialPackages.Basic)
            .Package(TutorialPackageIds.BpGameGuidanceEndBasic)
            .Package(MainWindow.TutorialPackages.NavigationScore)
            .Package(ScorePage.TutorialPackages.Basic)
            .Item(CreateMainWindowActivateAction())
            .Package(MainWindow.TutorialPackages.NewGameBasic)
            .Package(MainWindow.TutorialPackages.GlobalBanCarryOver)
            .Dialogue(string.Empty, NeoBpsysTutorialTexts.FirstRunEndingDialogue());

        registrar.RegisterFlow(builder.Build());
    }

    private static ActionFlowItem CreateMainWindowActivateAction() =>
        new()
        {
            ActionAsync = async (_, cancellationToken) =>
            {
                var window = Application.Current?.MainWindow;
                window?.Activate();
                if (window != null)
                {
                    await window.Dispatcher.InvokeAsync(
                        () => { },
                        System.Windows.Threading.DispatcherPriority.ContextIdle,
                        cancellationToken);
                }

                await Task.Delay(TutorialTransitionDelays.WindowSwitchSettleDelay, cancellationToken);
            }
        };
}
