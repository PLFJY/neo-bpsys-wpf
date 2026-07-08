using neo_bpsys_wpf.ProductTour;
using System.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in tutorial flows.
/// </summary>
public static class NeoBpsysTutorialFlows
{
    /// <summary>
     /// Registers flow definitions.
     /// </summary>
    /// <param name="flowRegistry">Flow registry.</param>
    public static void Register(ITutorialFlowRegistry flowRegistry)
    {
        flowRegistry.Register(CreatePhase4ANavigationProbeFlow());
        flowRegistry.Register(CreatePhase4RealTargetProbeFlow());
    }

    /// <summary>
    /// Creates the navigation probe flow definition.
    /// </summary>
    /// <returns>The navigation probe flow definition.</returns>
    public static TutorialFlowDefinition CreatePhase4ANavigationProbeFlow() =>
        TutorialFlowBuilder.Create(TutorialFlowIds.Phase4ANavigationProbe)
            .Version(1)
            .Dialogue(string.Empty, "现在我们验证左侧导航栏的教程引导。")
            .Package(MainWindow.TutorialPackages.NavigationTeamInfo)
            .Dialogue(string.Empty, "导航引导验证完成。")
            .Build();

    /// <summary>
    /// Creates the real target probe flow definition.
    /// </summary>
    /// <returns>The real target probe flow definition.</returns>
    public static TutorialFlowDefinition CreatePhase4RealTargetProbeFlow() =>
        TutorialFlowBuilder.Create(TutorialFlowIds.Phase4RealTargetProbe)
            .Version(1)
            .Dialogue(string.Empty, "现在开始验证真实页面目标和操作信号。")
            .Package(MainWindow.TutorialPackages.NavigationFrontManage)
            .Package(FrontedWindowsView.TutorialPackages.BpWindowLaunchBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(MainWindow.TutorialPackages.NavigationTeamInfo)
            .Package(TeamInfoPage.TutorialPackages.TeamNameBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(MainWindow.TutorialPackages.TeamSummaryBasic)
            .Package(MainWindow.TutorialPackages.GameProgressBo1FirstHalf)
            .Package(TutorialPackageIds.BpGameGuidanceStartBasic)
            .Dialogue(string.Empty, "真实目标验证完成。完整教学流程会在后续版本中提供。")
            .Build();

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
