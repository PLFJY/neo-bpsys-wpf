using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in tutorial page sequences.
/// </summary>
public static class NeoBpsysTutorialSequences
{
    /// <summary>
    /// Registers PageKey to package sequence mappings.
    /// </summary>
    /// <param name="sequenceRegistry">Sequence registry.</param>
    public static void Register(ITutorialSequenceRegistry sequenceRegistry)
    {
        foreach (var (pageKey, packages) in GetSequences())
        {
            sequenceRegistry.RegisterSequence(pageKey, packages);
        }
    }

    /// <summary>
    /// Gets all built-in page sequences.
    /// </summary>
    /// <returns>Page sequence definitions.</returns>
    public static IReadOnlyList<(string PageKey, string[] Packages)> GetSequences() =>
    [
        (TutorialPageKeys.Main,
        [
            TutorialPackageIds.MainNavigationFrontManage,
            TutorialPackageIds.MainNavigationTeamInfo,
            TutorialPackageIds.MainNavigationScore,
            TutorialPackageIds.MainNavigationSmartBp,
            TutorialPackageIds.MainNavigationDesignerV3,
            TutorialPackageIds.MainTeamSummaryBasic,
            TutorialPackageIds.MainNavigationBasic
        ]),
        (TutorialPageKeys.TeamInfo,
        [
            TutorialPackageIds.TeamInfoTeamNameBasic,
            TutorialPackageIds.TeamInfoJsonImportPreset,
            TutorialPackageIds.TeamInfoPlayerManage,
            TutorialPackageIds.TeamInfoBasic,
            TutorialPackageIds.TeamInfoJsonImport,
            TutorialPackageIds.TeamInfoAdvanced
        ]),
        (TutorialPageKeys.BpShared,
        [
            TutorialPackageIds.BpSharedBasic,
            TutorialPackageIds.BpCharacterSelectorBasic,
            TutorialPackageIds.BpPickCharacterBasic,
            TutorialPackageIds.BpGlobalBanRecordBasic,
            TutorialPackageIds.BpCharacterSelectorAdvanced
        ]),
        (TutorialPageKeys.BpGameGuidance,
        [
            TutorialPackageIds.BpGameGuidanceStartBasic,
            TutorialPackageIds.MapBpCompletionNextBasic,
            TutorialPackageIds.BpGameGuidanceBasic,
            TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf
        ]),
        (TutorialPageKeys.Score,
        [
            TutorialPackageIds.ScoreBasic,
            TutorialPackageIds.ScoreFrontedSync,
            TutorialPackageIds.ScoreAdvanced
        ]),
        (TutorialPageKeys.GameManage,
        [
            TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
            TutorialPackageIds.GameManageNewGameBasic,
            TutorialPackageIds.GameManageGlobalBanCarryOver,
            TutorialPackageIds.GameManageBasic,
            TutorialPackageIds.GameManageImportExport
        ]),
        (TutorialPageKeys.FrontManage,
        [
            TutorialPackageIds.FrontManageBpWindowLaunchBasic,
            TutorialPackageIds.FrontManageWindowsBasic,
            TutorialPackageIds.FrontManageLayoutPackagesBasic,
            TutorialPackageIds.FrontManageAdvanced
        ]),
        (TutorialPageKeys.DesignerV3,
        [
            TutorialPackageIds.DesignerV3LayoutEditBasic,
            TutorialPackageIds.DesignerV3BehaviorEditBasic,
            TutorialPackageIds.DesignerV3PackageImportExport,
            TutorialPackageIds.DesignerV3Advanced
        ]),
        (TutorialPageKeys.SmartBp,
        [
            TutorialPackageIds.SmartBpModuleShell,
            TutorialPackageIds.SmartBpCaptureBasic,
            TutorialPackageIds.SmartBpRegionEditorBasic,
            TutorialPackageIds.SmartBpFullBpFlowBasic,
            TutorialPackageIds.SmartBpPostGameAutoFill
        ])
    ];
}
