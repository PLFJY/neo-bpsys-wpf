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
        Assert.Equal("Page.Bp.Shared", TutorialPageKeys.BpShared);
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
        Assert.Equal("Page.Bp.GameGuidance.FlowBo1FirstHalf", TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf);
        Assert.Equal("Page.MapBp.CompletionNext.Basic", TutorialPackageIds.MapBpCompletionNextBasic);
        Assert.Equal("Page.Bp.PickCharacter.Basic", TutorialPackageIds.BpPickCharacterBasic);
        Assert.Equal("Page.Score.Basic", TutorialPackageIds.ScoreBasic);
        Assert.Equal("Page.Score.FrontedSync", TutorialPackageIds.ScoreFrontedSync);
        Assert.Equal("Page.Score.Advanced", TutorialPackageIds.ScoreAdvanced);
        Assert.Equal("Page.GameManage.Basic", TutorialPackageIds.GameManageBasic);
        Assert.Equal("Page.GameManage.GameProgress.Bo1FirstHalf", TutorialPackageIds.GameManageGameProgressBo1FirstHalf);
        Assert.Equal("Page.GameManage.NewGame.Basic", TutorialPackageIds.GameManageNewGameBasic);
        Assert.Equal("Page.GameManage.ImportExport", TutorialPackageIds.GameManageImportExport);
        Assert.Equal("Page.GameManage.GlobalBanCarryOver", TutorialPackageIds.GameManageGlobalBanCarryOver);
        Assert.Equal("Page.FrontManage.BpWindowLaunch.Basic", TutorialPackageIds.FrontManageBpWindowLaunchBasic);
        Assert.Equal("Page.FrontManage.Windows.Basic", TutorialPackageIds.FrontManageWindowsBasic);
        Assert.Equal("Page.FrontManage.LayoutPackages.Basic", TutorialPackageIds.FrontManageLayoutPackagesBasic);
        Assert.Equal("Page.FrontManage.Advanced", TutorialPackageIds.FrontManageAdvanced);
        Assert.Equal("Window.DesignerV3.LayoutEdit.Basic", TutorialPackageIds.DesignerV3LayoutEditBasic);
        Assert.Equal("Window.DesignerV3.BehaviorEdit.Basic", TutorialPackageIds.DesignerV3BehaviorEditBasic);
        Assert.Equal("Window.DesignerV3.PackageImportExport", TutorialPackageIds.DesignerV3PackageImportExport);
        Assert.Equal("Window.DesignerV3.Advanced", TutorialPackageIds.DesignerV3Advanced);
        Assert.Equal("Page.SmartBp.ModuleShell", TutorialPackageIds.SmartBpModuleShell);
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
        Assert.Equal("MapBpCompleted", TutorialSignalIds.MapBpCompleted);
        Assert.Equal("GuidanceNextClicked", TutorialSignalIds.GuidanceNextClicked);
        Assert.Equal("GuidanceStepChanged", TutorialSignalIds.GuidanceStepChanged);
        Assert.Equal("CharacterSelector.SearchCommitted", TutorialSignalIds.CharacterSelectorSearchCommitted);
        Assert.Equal("CharacterSelector.SelectionConfirmed", TutorialSignalIds.CharacterSelectorSelectionConfirmed);
        Assert.Equal("PickCharacterSelected.Survivor1", TutorialSignalIds.PickCharacterSelectedSurvivor1);
        Assert.Equal("PickCharacterSelected.Survivor2", TutorialSignalIds.PickCharacterSelectedSurvivor2);
        Assert.Equal("GlobalBanRecordUpdated", TutorialSignalIds.GlobalBanRecordUpdated);
        Assert.Equal("ScoreChanged", TutorialSignalIds.ScoreChanged);
        Assert.Equal("NewGameCreated", TutorialSignalIds.NewGameCreated);

        Assert.Equal("FrontendManageNavigationItem", TutorialTargetNames.FrontendManageNavigationItem);
        Assert.Equal("TeamInfoNavigationItem", TutorialTargetNames.TeamInfoNavigationItem);
        Assert.Equal("ScoreNavigationItem", TutorialTargetNames.ScoreNavigationItem);
        Assert.Equal("SmartBpNavigationItem", TutorialTargetNames.SmartBpNavigationItem);
        Assert.Equal("BpWindowLaunchButton", TutorialTargetNames.BpWindowLaunchButton);
        Assert.Equal("GameProgressComboBox", TutorialTargetNames.GameProgressComboBox);
        Assert.Equal("TeamNameInput", TutorialTargetNames.TeamNameInput);
        Assert.Equal("HomeTeamNameInput", TutorialTargetNames.HomeTeamNameInput);
        Assert.Equal("HomeTeamNameConfirmButton", TutorialTargetNames.HomeTeamNameConfirmButton);
        Assert.Equal("HomeTeamLogoButton", TutorialTargetNames.HomeTeamLogoButton);
        Assert.Equal("HomeTeamJsonImportButton", TutorialTargetNames.HomeTeamJsonImportButton);
        Assert.Equal("HomePlayerListPanel", TutorialTargetNames.HomePlayerListPanel);
        Assert.Equal("HomePlayerPositionPanel", TutorialTargetNames.HomePlayerPositionPanel);
        Assert.Equal("AwayTeamNameInput", TutorialTargetNames.AwayTeamNameInput);
        Assert.Equal("AwayTeamNameConfirmButton", TutorialTargetNames.AwayTeamNameConfirmButton);
        Assert.Equal("AwayTeamLogoButton", TutorialTargetNames.AwayTeamLogoButton);
        Assert.Equal("AwayTeamJsonImportButton", TutorialTargetNames.AwayTeamJsonImportButton);
        Assert.Equal("AwayPlayerListPanel", TutorialTargetNames.AwayPlayerListPanel);
        Assert.Equal("AwayPlayerPositionPanel", TutorialTargetNames.AwayPlayerPositionPanel);
        Assert.Equal("TeamSummaryCard", TutorialTargetNames.TeamSummaryCard);
        Assert.Equal("TeamSwapButton", TutorialTargetNames.TeamSwapButton);
        Assert.Equal("ImportTeamJsonButton", TutorialTargetNames.ImportTeamJsonButton);
        Assert.Equal("PlayerList", TutorialTargetNames.PlayerList);
        Assert.Equal("PlayerPositionPanel", TutorialTargetNames.PlayerPositionPanel);
        Assert.Equal("StopSmartBpAutoRecognitionButton", TutorialTargetNames.StopSmartBpAutoRecognitionButton);
        Assert.Equal("StartGameGuidanceButton", TutorialTargetNames.StartGameGuidanceButton);
        Assert.Equal("PreviousGuidanceStepButton", TutorialTargetNames.PreviousGuidanceStepButton);
        Assert.Equal("NextGuidanceStepButton", TutorialTargetNames.NextGuidanceStepButton);
        Assert.Equal("StopGameGuidanceButton", TutorialTargetNames.StopGameGuidanceButton);
        Assert.Equal("CharacterSelector", TutorialTargetNames.CharacterSelector);
        Assert.Equal("SurvivorPickPanel", TutorialTargetNames.SurvivorPickPanel);
        Assert.Equal("FirstSurvivorPickSelectorHost", TutorialTargetNames.FirstSurvivorPickSelectorHost);
        Assert.Equal("HunterPickSelectorHost", TutorialTargetNames.HunterPickSelectorHost);
        Assert.Equal("GlobalBanRecordPanel", TutorialTargetNames.GlobalBanRecordPanel);
        Assert.Equal("CurrentSurvivorGlobalBanRecordPanel", TutorialTargetNames.CurrentSurvivorGlobalBanRecordPanel);
        Assert.Equal("GlobalBanAutoRecordToggle", TutorialTargetNames.GlobalBanAutoRecordToggle);
        Assert.Equal("MapBpPanel", TutorialTargetNames.MapBpPanel);
        Assert.Equal("MapBpCompletedPanel", TutorialTargetNames.MapBpCompletedPanel);
        Assert.Equal("MapSelectorPanel", TutorialTargetNames.MapSelectorPanel);
        Assert.Equal("BanSurvivorSelectorPanel", TutorialTargetNames.BanSurvivorSelectorPanel);
        Assert.Equal("FirstBanSurvivorSelectorHost", TutorialTargetNames.FirstBanSurvivorSelectorHost);
        Assert.Equal("BanHunterSelectorPanel", TutorialTargetNames.BanHunterSelectorPanel);
        Assert.Equal("FirstBanHunterSelectorHost", TutorialTargetNames.FirstBanHunterSelectorHost);
        Assert.Equal("ScoreSelectorPanel", TutorialTargetNames.ScoreSelectorPanel);
        Assert.Equal("NewGameButton", TutorialTargetNames.NewGameButton);
    }
}
