using neo_bpsys_wpf.ProductTour;
using System.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in tutorial flows.
/// </summary>
public static class NeoBpsysTutorialFlows
{
    /// <summary>
    /// Package ids included by the standard first-run flow.
    /// </summary>
    public static readonly string[] FirstRunIncludedPackages =
    [
        TutorialPackageIds.MainNavigationFrontManage,
        TutorialPackageIds.FrontManageBpWindowLaunchBasic,
        TutorialPackageIds.MainNavigationTeamInfo,
        TutorialPackageIds.TeamInfoTeamNameBasic,
        TutorialPackageIds.MainTeamSummaryBasic,
        TutorialPackageIds.TeamInfoJsonImportPreset,
        TutorialPackageIds.TeamInfoPlayerManage,
        TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
        TutorialPackageIds.BpGameGuidanceStartBasic,
        TutorialPackageIds.MapBpCompletionNextBasic,
        TutorialPackageIds.BpCharacterSelectorBasic,
        TutorialPackageIds.BpPickCharacterBasic,
        TutorialPackageIds.BpGlobalBanRecordBasic,
        TutorialPackageIds.MainNavigationScore,
        TutorialPackageIds.ScoreBasic,
        TutorialPackageIds.GameManageNewGameBasic,
        TutorialPackageIds.GameManageGlobalBanCarryOver
    ];

    /// <summary>
    /// Registers flow definitions.
    /// </summary>
    /// <param name="flowRegistry">Flow registry.</param>
    public static void Register(ITutorialFlowRegistry flowRegistry)
    {
        flowRegistry.Register(CreateFirstRunFlow());
        flowRegistry.Register(CreatePhase4ANavigationProbeFlow());
        flowRegistry.Register(CreatePhase4RealTargetProbeFlow());
    }

    /// <summary>
    /// Creates the standard first-run flow definition.
    /// </summary>
    /// <returns>The first-run flow definition.</returns>
    public static TutorialFlowDefinition CreateFirstRunFlow()
    {
        var builder = TutorialFlowBuilder.Create(TutorialFlowIds.FirstRunStandardBp)
            .Version(1);

        foreach (var packageId in FirstRunIncludedPackages)
        {
            builder.Include(packageId);
        }

        builder
            .Dialogue("neo-bpsys-wpf", NeoBpsysTutorialTexts.FirstRunOpeningDialogue())
            .Package(TutorialPackageIds.MainNavigationFrontManage)
            .Package(TutorialPackageIds.FrontManageBpWindowLaunchBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(TutorialPackageIds.MainNavigationTeamInfo)
            .Package(TutorialPackageIds.TeamInfoTeamNameBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(TutorialPackageIds.MainTeamSummaryBasic)
            .Package(TutorialPackageIds.TeamInfoJsonImportPreset)
            .Package(TutorialPackageIds.TeamInfoPlayerManage)
            .Item(CreateMainWindowActivateAction())
            .Package(TutorialPackageIds.GameManageGameProgressBo1FirstHalf)
            .Package(TutorialPackageIds.BpGameGuidanceStartBasic)
            .Package(TutorialPackageIds.MapBpCompletionNextBasic)
            .Package(TutorialPackageIds.BpCharacterSelectorBasic)
            .Package(TutorialPackageIds.BpPickCharacterBasic)
            .Package(TutorialPackageIds.BpGlobalBanRecordBasic)
            .Package(TutorialPackageIds.MainNavigationScore)
            .Package(TutorialPackageIds.ScoreBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(TutorialPackageIds.GameManageNewGameBasic)
            .Package(TutorialPackageIds.GameManageGlobalBanCarryOver)
            .Dialogue("neo-bpsys-wpf", NeoBpsysTutorialTexts.FirstRunEndingDialogue());

        return builder.Build();
    }

    /// <summary>
    /// Creates the Phase 4A navigation probe flow definition.
    /// </summary>
    /// <returns>The Phase 4A navigation probe flow definition.</returns>
    public static TutorialFlowDefinition CreatePhase4ANavigationProbeFlow() =>
        TutorialFlowBuilder.Create(TutorialFlowIds.Phase4ANavigationProbe)
            .Version(1)
            .Dialogue("neo-bpsys-wpf", "现在我们验证左侧导航栏的教程引导。")
            .Package(TutorialPackageIds.MainNavigationTeamInfo)
            .Dialogue("neo-bpsys-wpf", "导航引导验证完成。")
            .Build();

    /// <summary>
    /// Creates the Phase 4 real target probe flow definition.
    /// </summary>
    /// <returns>The Phase 4 real target probe flow definition.</returns>
    public static TutorialFlowDefinition CreatePhase4RealTargetProbeFlow() =>
        TutorialFlowBuilder.Create(TutorialFlowIds.Phase4RealTargetProbe)
            .Version(1)
            .Dialogue("neo-bpsys-wpf", "现在开始验证真实页面目标和操作信号。")
            .Package(TutorialPackageIds.MainNavigationFrontManage)
            .Package(TutorialPackageIds.FrontManageBpWindowLaunchBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(TutorialPackageIds.MainNavigationTeamInfo)
            .Package(TutorialPackageIds.TeamInfoTeamNameBasic)
            .Item(CreateMainWindowActivateAction())
            .Package(TutorialPackageIds.MainTeamSummaryBasic)
            .Package(TutorialPackageIds.GameManageGameProgressBo1FirstHalf)
            .Package(TutorialPackageIds.BpGameGuidanceStartBasic)
            .Dialogue("neo-bpsys-wpf", "真实目标验证完成。完整教学流程会在教学沙盒完成后接入。")
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
