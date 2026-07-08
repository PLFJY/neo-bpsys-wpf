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
        Assert.Equal("Page.TeamInfo.Basic", TutorialPackageIds.TeamInfoBasic);
        Assert.Equal("Page.TeamInfo.JsonImport", TutorialPackageIds.TeamInfoJsonImport);
        Assert.Equal("Page.TeamInfo.PlayerManage", TutorialPackageIds.TeamInfoPlayerManage);
        Assert.Equal("Page.TeamInfo.Advanced", TutorialPackageIds.TeamInfoAdvanced);
        Assert.Equal("Page.Bp.Shared.Basic", TutorialPackageIds.BpSharedBasic);
        Assert.Equal("Page.Bp.CharacterSelector.Basic", TutorialPackageIds.BpCharacterSelectorBasic);
        Assert.Equal("Page.Bp.GlobalBanRecord.Basic", TutorialPackageIds.BpGlobalBanRecordBasic);
        Assert.Equal("Page.Bp.CharacterSelector.Advanced", TutorialPackageIds.BpCharacterSelectorAdvanced);
        Assert.Equal("Page.Bp.GameGuidance.Basic", TutorialPackageIds.BpGameGuidanceBasic);
        Assert.Equal("Page.Bp.GameGuidance.FlowBo1FirstHalf", TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf);
        Assert.Equal("Page.Score.Basic", TutorialPackageIds.ScoreBasic);
        Assert.Equal("Page.Score.FrontedSync", TutorialPackageIds.ScoreFrontedSync);
        Assert.Equal("Page.Score.Advanced", TutorialPackageIds.ScoreAdvanced);
        Assert.Equal("Page.GameManage.Basic", TutorialPackageIds.GameManageBasic);
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
        Assert.Equal("GlobalBanRecordUpdated", TutorialSignalIds.GlobalBanRecordUpdated);
        Assert.Equal("ScoreChanged", TutorialSignalIds.ScoreChanged);
        Assert.Equal("NewGameCreated", TutorialSignalIds.NewGameCreated);

        Assert.Equal("BpWindowLaunchButton", TutorialTargetNames.BpWindowLaunchButton);
        Assert.Equal("GameProgressComboBox", TutorialTargetNames.GameProgressComboBox);
        Assert.Equal("TeamNameInput", TutorialTargetNames.TeamNameInput);
        Assert.Equal("TeamSummaryCard", TutorialTargetNames.TeamSummaryCard);
        Assert.Equal("ImportTeamJsonButton", TutorialTargetNames.ImportTeamJsonButton);
        Assert.Equal("PlayerList", TutorialTargetNames.PlayerList);
        Assert.Equal("PlayerPositionPanel", TutorialTargetNames.PlayerPositionPanel);
        Assert.Equal("StopSmartBpAutoRecognitionButton", TutorialTargetNames.StopSmartBpAutoRecognitionButton);
        Assert.Equal("StartGameGuidanceButton", TutorialTargetNames.StartGameGuidanceButton);
        Assert.Equal("PreviousGuidanceStepButton", TutorialTargetNames.PreviousGuidanceStepButton);
        Assert.Equal("NextGuidanceStepButton", TutorialTargetNames.NextGuidanceStepButton);
        Assert.Equal("StopGameGuidanceButton", TutorialTargetNames.StopGameGuidanceButton);
        Assert.Equal("CharacterSelector", TutorialTargetNames.CharacterSelector);
        Assert.Equal("GlobalBanRecordPanel", TutorialTargetNames.GlobalBanRecordPanel);
        Assert.Equal("ScoreSelectorPanel", TutorialTargetNames.ScoreSelectorPanel);
        Assert.Equal("NewGameButton", TutorialTargetNames.NewGameButton);
    }
}
