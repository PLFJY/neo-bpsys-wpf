using System.Windows;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using MainWindowType = neo_bpsys_wpf.Views.Windows.MainWindow;

namespace neo_bpsys_wpf;

/// <summary>
/// 应用级产品导览定义。
/// </summary>
public partial class App : IAppTutorial<App>
{
    /// <inheritdoc />
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.Flow(TutorialFlowIds.FirstRunStandardBp)
            .Version(1)
            .Step(new DialogueFlowItem
            {
                Speaker = string.Empty,
                LinesKey = "Dialogue.FirstRun.Opening"
            })
            .Step(MainWindowType.Tours.NavigationFrontManage)
            .Step(FrontedWindowsView.Tours.BpWindowLaunchBasic)
            .Step(CreateMainWindowActivateAction())
            .Step(MainWindowType.Tours.NavigationTeamInfo)
            .Step(TeamInfoPage.Tours.TeamNameBasic)
            .Step(CreateMainWindowActivateAction())
            .Step(MainWindowType.Tours.TeamSummaryBasic)
            .Step(TeamInfoPage.Tours.JsonImportPreset)
            .Step(TeamInfoPage.Tours.PlayerManage)
            .Step(CreateMainWindowActivateAction())
            .Step(MainWindowType.Tours.GameProgressBo1FirstHalf)
            .Step(MainWindowType.Tours.BpGameGuidanceStartBasic)
            .Step(MainWindowType.Tours.BpGameGuidanceCurrentStepBasic)
            .Step(MainWindowType.Tours.MapBpBanMapOperationBasic)
            .Step(MainWindowType.Tours.MapBpNextToPickMapBasic)
            .Step(MainWindowType.Tours.MapBpPickMapBasic)
            .Step(BanSurPage.Tours.CharacterSelectorBasic)
            .Step(PickPage.Tours.SelectFourSurvivorsBasic)
            .Step(PickPage.Tours.GlobalBanRecordBasic)
            .Step(PickPage.Tours.CharacterChangerBasic)
            .Step(TalentPage.Tours.Basic)
            .Step(MainWindowType.Tours.BpGameGuidanceEndBasic)
            .Step(MainWindowType.Tours.NavigationScore)
            .Step(ScorePage.Tours.Basic)
            .Step(CreateMainWindowActivateAction())
            .Step(MainWindowType.Tours.NewGameBasic)
            .Step(MainWindowType.Tours.NextGameBasic)
            .Step(MainWindowType.Tours.GlobalBanCarryOver)
            .Step(new DialogueFlowItem
            {
                Speaker = string.Empty,
                LinesKey = "Dialogue.FirstRun.Ending"
            })
            .Build();
    }

    private static ActionFlowItem CreateMainWindowActivateAction() =>
        new()
        {
            ActionAsync = async (_, cancellationToken) =>
            {
                var window = Current?.MainWindow;
                window?.Activate();
                if (window != null)
                {
                    await window.Dispatcher.InvokeAsync(
                        () => { },
                        System.Windows.Threading.DispatcherPriority.ContextIdle,
                        cancellationToken);
                }

            }
        };
}
