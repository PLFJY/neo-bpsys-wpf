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
        TutorialPackageIds.MainNavigationBasic,
        TutorialPackageIds.FrontManageBpWindowLaunchBasic,
        TutorialPackageIds.GameManageBasic,
        TutorialPackageIds.TeamInfoBasic,
        TutorialPackageIds.TeamInfoJsonImport,
        TutorialPackageIds.TeamInfoPlayerManage,
        TutorialPackageIds.BpGameGuidanceBasic,
        TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf,
        TutorialPackageIds.BpSharedBasic,
        TutorialPackageIds.BpCharacterSelectorBasic,
        TutorialPackageIds.BpGlobalBanRecordBasic,
        TutorialPackageIds.ScoreBasic,
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

        builder.Dialogue("neo-bpsys-wpf", NeoBpsysTutorialTexts.FirstRunOpeningDialogue());
        foreach (var packageId in FirstRunIncludedPackages)
        {
            builder.Package(packageId);
        }

        builder.Dialogue("neo-bpsys-wpf", NeoBpsysTutorialTexts.FirstRunEndingDialogue());
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
            .Package(TutorialPackageIds.MainNavigationBasic)
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
            .Package(TutorialPackageIds.MainNavigationBasic)
            .Package(TutorialPackageIds.TeamInfoBasic)
            .Item(new ActionFlowItem
            {
                ActionAsync = (_, _) =>
                {
                    Application.Current?.MainWindow?.Activate();
                    return Task.CompletedTask;
                }
            })
            .Package(TutorialPackageIds.GameManageBasic)
            .Package(TutorialPackageIds.BpGameGuidanceBasic)
            .Dialogue("neo-bpsys-wpf", "真实目标验证完成。完整教学流程会在教学沙盒完成后接入。")
            .Build();
}
