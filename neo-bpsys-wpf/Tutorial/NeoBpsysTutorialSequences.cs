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
        (TutorialPageKeys.TeamInfo,
        [
            TutorialPackageIds.TeamInfoBasic,
            TutorialPackageIds.TeamInfoJsonImport,
            TutorialPackageIds.TeamInfoPlayerManage,
            TutorialPackageIds.TeamInfoAdvanced
        ]),
        (TutorialPageKeys.BpShared,
        [
            TutorialPackageIds.BpSharedBasic,
            TutorialPackageIds.BpCharacterSelectorBasic,
            TutorialPackageIds.BpGlobalBanRecordBasic,
            TutorialPackageIds.BpCharacterSelectorAdvanced
        ]),
        (TutorialPageKeys.BpGameGuidance,
        [
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
            TutorialPackageIds.GameManageBasic,
            TutorialPackageIds.GameManageImportExport,
            TutorialPackageIds.GameManageGlobalBanCarryOver
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
