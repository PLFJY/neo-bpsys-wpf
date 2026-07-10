using neo_bpsys_wpf.Tutorial;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests built-in neo-bpsys-wpf tutorial id constants.
/// </summary>
public sealed class NeoBpsysTutorialIdsTest
{
    [Fact]
    public void ConstantsKeepPersistedStringValues()
    {
        Assert.Equal("Flow.FirstRun.StandardBp", TutorialFlowIds.FirstRunStandardBp);
        Assert.Equal("Flow.Phase4A.NavigationProbe", TutorialFlowIds.Phase4ANavigationProbe);
        Assert.Equal("Flow.Phase4.RealTargetProbe", TutorialFlowIds.Phase4RealTargetProbe);

        Assert.Equal("Page.Main", TutorialPageKeys.Main);
        Assert.Equal("Page.TeamInfo", TutorialPageKeys.TeamInfo);
        Assert.Equal("Page.Bp.GameGuidance", TutorialPageKeys.BpGameGuidance);
        Assert.Equal("Page.Score", TutorialPageKeys.Score);
        Assert.Equal("Page.GameManage", TutorialPageKeys.GameManage);
        Assert.Equal("Page.FrontManage", TutorialPageKeys.FrontManage);
        Assert.Equal("Window.DesignerV3", TutorialPageKeys.DesignerV3);
        Assert.Equal("Page.SmartBp", TutorialPageKeys.SmartBp);

        Assert.Equal("Page.Main.Navigation.Basic", TutorialPackageIds.MainNavigationBasic);
        Assert.Equal("Page.Main.Navigation.FrontManage", TutorialPackageIds.MainNavigationFrontManage);
        Assert.Equal("Page.Main.Navigation.TeamInfo", TutorialPackageIds.MainNavigationTeamInfo);
        Assert.Equal("Page.Main.Navigation.Score", TutorialPackageIds.MainNavigationScore);
        Assert.Equal("Page.Main.Navigation.SmartBp", TutorialPackageIds.MainNavigationSmartBp);
        Assert.Equal("Page.Main.Navigation.DesignerV3", TutorialPackageIds.MainNavigationDesignerV3);
        Assert.Equal("Page.Main.TeamSummary.Basic", TutorialPackageIds.MainTeamSummaryBasic);
        Assert.Equal("Page.TeamInfo.Basic", TutorialPackageIds.TeamInfoBasic);
        Assert.Equal("Page.TeamInfo.TeamName.Basic", TutorialPackageIds.TeamInfoTeamNameBasic);
        Assert.Equal("Page.TeamInfo.JsonImport", TutorialPackageIds.TeamInfoJsonImport);
        Assert.Equal("Page.TeamInfo.JsonImport.Preset", TutorialPackageIds.TeamInfoJsonImportPreset);
        Assert.Equal("Page.TeamInfo.PlayerManage", TutorialPackageIds.TeamInfoPlayerManage);
        Assert.Equal("Page.TeamInfo.Advanced", TutorialPackageIds.TeamInfoAdvanced);
        Assert.Equal("Page.Bp.Shared.Basic", TutorialPackageIds.BpSharedBasic);
        Assert.Equal("Page.Bp.CharacterSelector.Basic", TutorialPackageIds.BpCharacterSelectorBasic);
        Assert.Equal("Page.Bp.GlobalBanRecord.Basic", TutorialPackageIds.BpGlobalBanRecordBasic);
        Assert.Equal("Page.Bp.CharacterSelector.Advanced", TutorialPackageIds.BpCharacterSelectorAdvanced);
        Assert.Equal("Page.Bp.GameGuidance.Basic", TutorialPackageIds.BpGameGuidanceBasic);
        Assert.Equal("Page.Bp.GameGuidance.Start.Basic", TutorialPackageIds.BpGameGuidanceStartBasic);
        Assert.Equal("Page.Bp.GameGuidance.CurrentStep.Basic", TutorialPackageIds.BpGameGuidanceCurrentStepBasic);
        Assert.Equal("Page.Bp.GameGuidance.End.Basic", TutorialPackageIds.BpGameGuidanceEndBasic);
        Assert.Equal("Page.MapBp.BanMapOperation.Basic", TutorialPackageIds.MapBpBanMapOperationBasic);
        Assert.Equal("Page.MapBp.NextToPickMap.Basic", TutorialPackageIds.MapBpNextToPickMapBasic);
        Assert.Equal("Page.Bp.PickCharacter.Basic", TutorialPackageIds.BpPickCharacterBasic);
        Assert.Equal("Page.Bp.Pick.SelectFourSurvivors.Basic", TutorialPackageIds.BpPickSelectFourSurvivorsBasic);
        Assert.Equal("Page.Bp.CharacterChanger.Basic", TutorialPackageIds.BpCharacterChangerBasic);
        Assert.Equal("Page.Bp.TalentTrait.Basic", TutorialPackageIds.BpTalentTraitBasic);
        Assert.Equal("Page.Score.Basic", TutorialPackageIds.ScoreBasic);
        Assert.Equal("Page.Score.FrontedSync", TutorialPackageIds.ScoreFrontedSync);
        Assert.Equal("Page.Score.Advanced", TutorialPackageIds.ScoreAdvanced);
        Assert.Equal("Page.GameManage.Basic", TutorialPackageIds.GameManageBasic);
        Assert.Equal("Page.GameManage.GameProgress.Bo1FirstHalf", TutorialPackageIds.GameManageGameProgressBo1FirstHalf);
        Assert.Equal("Page.GameManage.NewGame.Basic", TutorialPackageIds.GameManageNewGameBasic);
        Assert.Equal("Page.GameManage.ImportExport", TutorialPackageIds.GameManageImportExport);
        Assert.Equal("Page.GameManage.GlobalBanCarryOver", TutorialPackageIds.GameManageGlobalBanCarryOver);
        Assert.Equal("Page.FrontManage.BpWindowLaunch.Basic", TutorialPackageIds.FrontManageBpWindowLaunchBasic);
        Assert.Equal("Page.FrontManage.Overview", TutorialPackageIds.FrontManageOverview);
        Assert.Equal("Page.FrontManage.Windows.Basic", TutorialPackageIds.FrontManageWindowsBasic);
        Assert.Equal("Page.FrontManage.OpenDesigner", TutorialPackageIds.FrontManageOpenDesigner);
        Assert.Equal("Page.FrontManage.LayoutPackages.Basic", TutorialPackageIds.FrontManageLayoutPackagesBasic);
        Assert.Equal("Window.DesignerV3.Overview", TutorialPackageIds.DesignerV3Overview);
        Assert.Equal("Window.DesignerV3.LayoutEdit.Basic", TutorialPackageIds.DesignerV3LayoutEditBasic);
        Assert.Equal("Window.DesignerV3.PropertyPanel.Basic", TutorialPackageIds.DesignerV3PropertyPanelBasic);
        Assert.Equal("Window.DesignerV3.BehaviorEdit.Basic", TutorialPackageIds.DesignerV3BehaviorEditBasic);
        Assert.Equal("Window.DesignerV3.PackageImportExport", TutorialPackageIds.DesignerV3PackageImportExport);
        Assert.Equal("Page.SmartBp.ModuleShell", TutorialPackageIds.SmartBpModuleShell);
        Assert.Equal("Page.SmartBp.ModuleContent.Overview", TutorialPackageIds.SmartBpModuleContentOverview);
        Assert.Equal("Page.SmartBp.Capture.Basic", TutorialPackageIds.SmartBpCaptureBasic);
        Assert.Equal("Page.SmartBp.RegionEditor.Basic", TutorialPackageIds.SmartBpRegionEditorBasic);
        Assert.Equal("Page.SmartBp.FullBpFlow.Basic", TutorialPackageIds.SmartBpFullBpFlowBasic);
        Assert.Equal("Page.SmartBp.PostGameAutoFill", TutorialPackageIds.SmartBpPostGameAutoFill);

        Assert.Equal("Navigation.TeamInfo.Opened", TutorialSignalIds.NavigationTeamInfoOpened);
        Assert.Equal("Navigation.Home.Opened", TutorialSignalIds.NavigationHomeOpened);
        Assert.Equal("Navigation.Score.Opened", TutorialSignalIds.NavigationScoreOpened);
        Assert.Equal("Navigation.FrontManage.Opened", TutorialSignalIds.NavigationFrontManageOpened);
        Assert.Equal("Navigation.SmartBp.Opened", TutorialSignalIds.NavigationSmartBpOpened);
        Assert.Equal("Navigation.MapBp.Opened", TutorialSignalIds.NavigationMapBpOpened);
        Assert.Equal("Navigation.BanSurvivor.Opened", TutorialSignalIds.NavigationBanSurvivorOpened);
        Assert.Equal("Navigation.BanHunter.Opened", TutorialSignalIds.NavigationBanHunterOpened);
        Assert.Equal("Navigation.PickCharacter.Opened", TutorialSignalIds.NavigationPickCharacterOpened);
        Assert.Equal("BpWindowOpened", TutorialSignalIds.BpWindowOpened);
        Assert.Equal("GameProgressSelected.Bo1FirstHalf", TutorialSignalIds.GameProgressSelectedBo1FirstHalf);
        Assert.Equal("TeamNameConfirmed", TutorialSignalIds.TeamNameConfirmed);
        Assert.Equal("TeamJsonImported.Home", TutorialSignalIds.TeamJsonImportedHome);
        Assert.Equal("TeamJsonImported.Away", TutorialSignalIds.TeamJsonImportedAway);
        Assert.Equal("MemberStateChanged", TutorialSignalIds.MemberStateChanged);
        Assert.Equal("MemberPositionSwapped", TutorialSignalIds.MemberPositionSwapped);
        Assert.Equal("GameGuidanceStarted", TutorialSignalIds.GameGuidanceStarted);
        Assert.Equal("GuidanceNextClicked", TutorialSignalIds.GuidanceNextClicked);
        Assert.Equal("GuidanceStepChanged", TutorialSignalIds.GuidanceStepChanged);
        Assert.Equal("CharacterSelector.SearchCommitted", TutorialSignalIds.CharacterSelectorSearchCommitted);
        Assert.Equal("CharacterSelector.SelectionConfirmed", TutorialSignalIds.CharacterSelectorSelectionConfirmed);
        Assert.Equal("PickCharacterSelected.Survivor1", TutorialSignalIds.PickCharacterSelectedSurvivor1);
        Assert.Equal("PickCharacterSelected.Survivor2", TutorialSignalIds.PickCharacterSelectedSurvivor2);
        Assert.Equal("PickCharacterSelected.Survivor3", TutorialSignalIds.PickCharacterSelectedSurvivor3);
        Assert.Equal("PickCharacterSelected.Survivor4", TutorialSignalIds.PickCharacterSelectedSurvivor4);
        Assert.Equal("PickSurvivorSlotsCompleted", TutorialSignalIds.PickSurvivorSlotsCompleted);
        Assert.Equal("CharacterChangerApplied", TutorialSignalIds.CharacterChangerApplied);
        Assert.Equal("GameGuidanceStopped", TutorialSignalIds.GameGuidanceStopped);
        Assert.Equal("GlobalBanRecordUpdated", TutorialSignalIds.GlobalBanRecordUpdated);
        Assert.Equal("ScoreChanged", TutorialSignalIds.ScoreChanged);
        Assert.Equal("NewGameCreated", TutorialSignalIds.NewGameCreated);
        Assert.Equal("DesignerV3.Opened", TutorialSignalIds.DesignerV3Opened);
        Assert.Equal("FrontManage.OpenDesigner.Clicked", TutorialSignalIds.FrontManageOpenDesignerClicked);
        Assert.Equal("SmartBp.ModuleLoaded", TutorialSignalIds.SmartBpModuleLoaded);

        Assert.Equal("HomeTeamNameInput", TutorialTargetNames.HomeTeamNameInput);
        Assert.Equal("HomeTeamNameConfirmButton", TutorialTargetNames.HomeTeamNameConfirmButton);
        Assert.Equal("HomeTeamLogoButton", TutorialTargetNames.HomeTeamLogoButton);
        Assert.Equal("HomeTeamJsonImportButton", TutorialTargetNames.HomeTeamJsonImportButton);
        Assert.Equal("HomePlayerListPanel", TutorialTargetNames.HomePlayerListPanel);
        Assert.Equal("HomePlayerPositionPanel", TutorialTargetNames.HomePlayerPositionPanel);
        Assert.Equal("AwayTeamJsonImportButton", TutorialTargetNames.AwayTeamJsonImportButton);
        Assert.Equal("AwayPlayerListPanel", TutorialTargetNames.AwayPlayerListPanel);
        Assert.Equal("NextGuidanceStepButton", TutorialTargetNames.NextGuidanceStepButton);
        Assert.Equal("SurvivorPickSelectorGroupBorder", TutorialTargetNames.SurvivorPickSelectorGroupBorder);
        Assert.Equal("FirstSurvivorPickSelectorHost", TutorialTargetNames.FirstSurvivorPickSelectorHost);
        Assert.Equal("TalentTraitSelectorPanel", TutorialTargetNames.TalentTraitSelectorPanel);
        Assert.Equal("SurvivorTalentSelector", TutorialTargetNames.SurvivorTalentSelector);
        Assert.Equal("HunterTraitSelector", TutorialTargetNames.HunterTraitSelector);
        Assert.Equal("GlobalBanRecordPanel", TutorialTargetNames.GlobalBanRecordPanel);
        Assert.Equal("MapBanOperationBorder", TutorialTargetNames.MapBanOperationBorder);
        Assert.Equal("MapSelectorPanel", TutorialTargetNames.MapSelectorPanel);
        Assert.Equal("PreviewCanvas", TutorialTargetNames.PreviewCanvas);
        Assert.Equal("InteractionLayer", TutorialTargetNames.InteractionLayer);
        Assert.Equal("BehaviorPanelHost", TutorialTargetNames.BehaviorPanelHost);
        Assert.Equal("DesignerHelpButton", TutorialTargetNames.DesignerHelpButton);
        Assert.Equal("SmartBpWindowSelector", TutorialTargetNames.SmartBpWindowSelector);
        Assert.Equal("SmartBpStartCaptureButton", TutorialTargetNames.SmartBpStartCaptureButton);
        Assert.Equal("SmartBpPreviewButton", TutorialTargetNames.SmartBpPreviewButton);
        Assert.Equal("SmartBpPreviewPanel", TutorialTargetNames.SmartBpPreviewPanel);
        Assert.Equal("SmartBpStopCaptureButton", TutorialTargetNames.SmartBpStopCaptureButton);
        Assert.Equal("SmartBpRegionEditorButton", TutorialTargetNames.SmartBpRegionEditorButton);
        Assert.Equal("SmartBpRegionPreviewPanel", TutorialTargetNames.SmartBpRegionPreviewPanel);
        Assert.Equal("SmartBpRegionListPanel", TutorialTargetNames.SmartBpRegionListPanel);
        Assert.Equal("SmartBpSaveRegionButton", TutorialTargetNames.SmartBpSaveRegionButton);
        Assert.Equal("SmartBpStartFullBpFlowButton", TutorialTargetNames.SmartBpStartFullBpFlowButton);
    }
}
