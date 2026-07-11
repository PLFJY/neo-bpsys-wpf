using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in tutorial flows.
/// </summary>
public static class NeoBpsysTutorialFlows
{
    /// <summary>
    /// Registers flow definitions.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void Register(ITutorialBuilder builder)
    {
        builder.Flow(TutorialFlowIds.Phase4ANavigationProbe)
            .Version(1)
            .Step(new DialogueFlowItem { Speaker = string.Empty, LinesKey = "Dialogue.Probe.Navigation.Opening.Lines" })
            .Step(MainWindow.Tours.NavigationTeamInfo)
            .Step(new DialogueFlowItem { Speaker = string.Empty, LinesKey = "Dialogue.Probe.Navigation.Closing.Lines" })
            .Build();

        builder.Flow(TutorialFlowIds.Phase4RealTargetProbe)
            .Version(1)
            .Step(new DialogueFlowItem { Speaker = string.Empty, LinesKey = "Dialogue.Probe.RealTarget.Opening.Lines" })
            .Step(MainWindow.Tours.NavigationFrontManage)
            .Step(FrontedWindowsView.Tours.BpWindowLaunchBasic)
            .Step(CreateMainWindowActivateAction())
            .Step(MainWindow.Tours.NavigationTeamInfo)
            .Step(TeamInfoPage.Tours.TeamNameBasic)
            .Step(CreateMainWindowActivateAction())
            .Step(MainWindow.Tours.TeamSummaryBasic)
            .Step(MainWindow.Tours.GameProgressBo1FirstHalf)
            .Step(MainWindow.Tours.BpGameGuidanceStartBasic)
            .Step(new DialogueFlowItem { Speaker = string.Empty, LinesKey = "Dialogue.Probe.RealTarget.Closing.Lines" })
            .Build();
    }

    private static ActionFlowItem CreateMainWindowActivateAction() =>
        new()
        {
            ActionAsync = (_, _) =>
            {
                Application.Current?.MainWindow?.Activate();
                return Task.CompletedTask;
            }
        };
}
