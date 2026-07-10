namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Built-in tutorial flow ids.
/// </summary>
public static class TutorialFlowIds
{
    /// <summary>The standard first-run BP tutorial flow id.</summary>
    public const string FirstRunStandardBp = "Flow.FirstRun.StandardBp";

    /// <summary>Navigation probe tutorial flow id.</summary>
    public const string Phase4ANavigationProbe = "Flow.Phase4A.NavigationProbe";

    /// <summary>Real target probe tutorial flow id.</summary>
    public const string Phase4RealTargetProbe = "Flow.Phase4.RealTargetProbe";
}

/// <summary>
/// Built-in tutorial page keys.
/// </summary>
public static class TutorialPageKeys
{
    /// <summary>Main window page key.</summary>
    public const string Main = "Page.Main";

    /// <summary>Team info page key.</summary>
    public const string TeamInfo = "Page.TeamInfo";

    /// <summary>BP game guidance page key.</summary>
    public const string BpGameGuidance = "Page.Bp.GameGuidance";

    /// <summary>Score page key.</summary>
    public const string Score = "Page.Score";

    /// <summary>Game management page key.</summary>
    public const string GameManage = "Page.GameManage";

    /// <summary>Front management page key.</summary>
    public const string FrontManage = "Page.FrontManage";

    /// <summary>Designer v3 window key.</summary>
    public const string DesignerV3 = "Window.DesignerV3";

    /// <summary>Designer v3 behavior panel key.</summary>
    public const string DesignerV3BehaviorPanel = "Window.DesignerV3.BehaviorPanel";

    /// <summary>Designer v3 animation editor key.</summary>
    public const string DesignerV3AnimationEditor = "Window.DesignerV3.AnimationEditor";

    /// <summary>Smart BP page key.</summary>
    public const string SmartBp = "Page.SmartBp";
}

/// <summary>
/// Built-in tutorial package ids.
/// </summary>
public static class TutorialPackageIds
{
    /// <summary>Main navigation basic package id.</summary>
    public const string MainNavigationBasic = "Page.Main.Navigation.Basic";

    /// <summary>Main navigation front management package id.</summary>
    public const string MainNavigationFrontManage = "Page.Main.Navigation.FrontManage";

    /// <summary>Main navigation team info package id.</summary>
    public const string MainNavigationTeamInfo = "Page.Main.Navigation.TeamInfo";

    /// <summary>Main navigation score package id.</summary>
    public const string MainNavigationScore = "Page.Main.Navigation.Score";

    /// <summary>Main navigation Smart BP package id.</summary>
    public const string MainNavigationSmartBp = "Page.Main.Navigation.SmartBp";

    /// <summary>Main navigation Designer v3 package id.</summary>
    public const string MainNavigationDesignerV3 = "Page.Main.Navigation.DesignerV3";

    /// <summary>Main team summary package id.</summary>
    public const string MainTeamSummaryBasic = "Page.Main.TeamSummary.Basic";

    /// <summary>Team info basic package id.</summary>
    public const string TeamInfoBasic = "Page.TeamInfo.Basic";

    /// <summary>Team name basic package id.</summary>
    public const string TeamInfoTeamNameBasic = "Page.TeamInfo.TeamName.Basic";

    /// <summary>Team JSON import package id.</summary>
    public const string TeamInfoJsonImport = "Page.TeamInfo.JsonImport";

    /// <summary>Preset team JSON import package id.</summary>
    public const string TeamInfoJsonImportPreset = "Page.TeamInfo.JsonImport.Preset";

    /// <summary>Team player management package id.</summary>
    public const string TeamInfoPlayerManage = "Page.TeamInfo.PlayerManage";

    /// <summary>Team info advanced package id.</summary>
    public const string TeamInfoAdvanced = "Page.TeamInfo.Advanced";

    /// <summary>Shared BP basic package id.</summary>
    public const string BpSharedBasic = "Page.Bp.Shared.Basic";

    /// <summary>Character selector basic package id.</summary>
    public const string BpCharacterSelectorBasic = "Page.Bp.CharacterSelector.Basic";

    /// <summary>Global ban record basic package id.</summary>
    public const string BpGlobalBanRecordBasic = "Page.Bp.GlobalBanRecord.Basic";

    /// <summary>Character selector advanced package id.</summary>
    public const string BpCharacterSelectorAdvanced = "Page.Bp.CharacterSelector.Advanced";

    /// <summary>Game guidance basic package id.</summary>
    public const string BpGameGuidanceBasic = "Page.Bp.GameGuidance.Basic";

    /// <summary>Game guidance start package id.</summary>
    public const string BpGameGuidanceStartBasic = "Page.Bp.GameGuidance.Start.Basic";

    /// <summary>Game guidance current-step package id.</summary>
    public const string BpGameGuidanceCurrentStepBasic = "Page.Bp.GameGuidance.CurrentStep.Basic";

    /// <summary>Game guidance end package id.</summary>
    public const string BpGameGuidanceEndBasic = "Page.Bp.GameGuidance.End.Basic";

    /// <summary>Map BP ban-map operation package id.</summary>
    public const string MapBpBanMapOperationBasic = "Page.MapBp.BanMapOperation.Basic";

    /// <summary>Map BP pick-map operation package id.</summary>
    public const string MapBpPickMapOperationBasic = "Page.MapBp.PickMapOperation.Basic";

    /// <summary>Map BP next-to-pick-map package id.</summary>
    public const string MapBpNextToPickMapBasic = "Page.MapBp.NextToPickMap.Basic";

    /// <summary>BP survivor pick character package id.</summary>
    public const string BpPickCharacterBasic = "Page.Bp.PickCharacter.Basic";

    /// <summary>BP survivor four-character selection package id.</summary>
    public const string BpPickSelectFourSurvivorsBasic = "Page.Bp.Pick.SelectFourSurvivors.Basic";

    /// <summary>BP character changer package id.</summary>
    public const string BpCharacterChangerBasic = "Page.Bp.CharacterChanger.Basic";

    /// <summary>BP talent and trait package id.</summary>
    public const string BpTalentTraitBasic = "Page.Bp.TalentTrait.Basic";

    /// <summary>Score basic package id.</summary>
    public const string ScoreBasic = "Page.Score.Basic";

    /// <summary>Score fronted sync package id.</summary>
    public const string ScoreFrontedSync = "Page.Score.FrontedSync";

    /// <summary>Score advanced package id.</summary>
    public const string ScoreAdvanced = "Page.Score.Advanced";

    /// <summary>Game management basic package id.</summary>
    public const string GameManageBasic = "Page.GameManage.Basic";

    /// <summary>BO1 first-half game progress package id.</summary>
    public const string GameManageGameProgressBo1FirstHalf = "Page.GameManage.GameProgress.Bo1FirstHalf";

    /// <summary>New game basic package id.</summary>
    public const string GameManageNewGameBasic = "Page.GameManage.NewGame.Basic";

    /// <summary>Game management import/export package id.</summary>
    public const string GameManageImportExport = "Page.GameManage.ImportExport";

    /// <summary>Global ban carry-over package id.</summary>
    public const string GameManageGlobalBanCarryOver = "Page.GameManage.GlobalBanCarryOver";

    /// <summary>Front management BP window launch package id.</summary>
    public const string FrontManageBpWindowLaunchBasic = "Page.FrontManage.BpWindowLaunch.Basic";

    /// <summary>Front management overview package id.</summary>
    public const string FrontManageOverview = "Page.FrontManage.Overview";

    /// <summary>Front management windows package id.</summary>
    public const string FrontManageWindowsBasic = "Page.FrontManage.Windows.Basic";

    /// <summary>Front management open Designer v3 package id.</summary>
    public const string FrontManageOpenDesigner = "Page.FrontManage.OpenDesigner";

    /// <summary>Front management layout packages package id.</summary>
    public const string FrontManageLayoutPackagesBasic = "Page.FrontManage.LayoutPackages.Basic";

    /// <summary>Designer v3 overview package id.</summary>
    public const string DesignerV3Overview = "Window.DesignerV3.Overview";

    /// <summary>Designer v3 layout edit package id.</summary>
    public const string DesignerV3LayoutEditBasic = "Window.DesignerV3.LayoutEdit.Basic";

    /// <summary>Designer v3 property panel package id.</summary>
    public const string DesignerV3PropertyPanelBasic = "Window.DesignerV3.PropertyPanel.Basic";

    /// <summary>Designer v3 behavior edit package id.</summary>
    public const string DesignerV3BehaviorEditBasic = "Window.DesignerV3.BehaviorEdit.Basic";

    /// <summary>Designer v3 package import/export package id.</summary>
    public const string DesignerV3PackageImportExport = "Window.DesignerV3.PackageImportExport";

    /// <summary>Designer v3 help package id.</summary>
    public const string DesignerV3HelpBasic = "Window.DesignerV3.Help.Basic";

    /// <summary>Designer v3 behavior panel overview package id.</summary>
    public const string DesignerV3BehaviorPanelOverview = "Window.DesignerV3.BehaviorPanel.Overview";

    /// <summary>Designer v3 behavior panel trigger package id.</summary>
    public const string DesignerV3BehaviorPanelTriggerBasic = "Window.DesignerV3.BehaviorPanel.Trigger.Basic";

    /// <summary>Designer v3 behavior panel action package id.</summary>
    public const string DesignerV3BehaviorPanelActionBasic = "Window.DesignerV3.BehaviorPanel.Action.Basic";

    /// <summary>Designer v3 behavior panel help package id.</summary>
    public const string DesignerV3BehaviorPanelHelpBasic = "Window.DesignerV3.BehaviorPanel.Help.Basic";

    /// <summary>Designer v3 animation editor overview package id.</summary>
    public const string DesignerV3AnimationEditorOverview = "Window.DesignerV3.AnimationEditor.Overview";

    /// <summary>Designer v3 animation editor timeline package id.</summary>
    public const string DesignerV3AnimationEditorTimelineBasic = "Window.DesignerV3.AnimationEditor.Timeline.Basic";

    /// <summary>Designer v3 animation editor key frame package id.</summary>
    public const string DesignerV3AnimationEditorKeyFrameBasic = "Window.DesignerV3.AnimationEditor.KeyFrame.Basic";

    /// <summary>Designer v3 animation editor preview package id.</summary>
    public const string DesignerV3AnimationEditorPreviewBasic = "Window.DesignerV3.AnimationEditor.Preview.Basic";

    /// <summary>Designer v3 animation editor help package id.</summary>
    public const string DesignerV3AnimationEditorHelpBasic = "Window.DesignerV3.AnimationEditor.Help.Basic";

    /// <summary>Smart BP module shell package id.</summary>
    public const string SmartBpModuleShell = "Page.SmartBp.ModuleShell";

    /// <summary>Smart BP module content overview package id.</summary>
    public const string SmartBpModuleContentOverview = "Page.SmartBp.ModuleContent.Overview";

    /// <summary>Smart BP OCR model download and management package id.</summary>
    public const string SmartBpOcrModelDownloadBasic = "Page.SmartBp.OcrModelDownload.Basic";

    /// <summary>Smart BP capture package id.</summary>
    public const string SmartBpCaptureBasic = "Page.SmartBp.Capture.Basic";

    /// <summary>Smart BP region editor package id.</summary>
    public const string SmartBpRegionEditorBasic = "Page.SmartBp.RegionEditor.Basic";

    /// <summary>Smart BP full BP flow package id.</summary>
    public const string SmartBpFullBpFlowBasic = "Page.SmartBp.FullBpFlow.Basic";

    /// <summary>Smart BP post-game auto-fill package id.</summary>
    public const string SmartBpPostGameAutoFill = "Page.SmartBp.PostGameAutoFill";

    public const string NextGameBasic = "Page.Bp.GameGuidance.NextGameBasic.Basic";
}

/// <summary>
/// Built-in tutorial signal ids.
/// </summary>
public static class TutorialSignalIds
{
    /// <summary>Team info navigation opened signal id.</summary>
    public const string NavigationTeamInfoOpened = "Navigation.TeamInfo.Opened";

    /// <summary>Home navigation opened signal id.</summary>
    public const string NavigationHomeOpened = "Navigation.Home.Opened";

    /// <summary>Score navigation opened signal id.</summary>
    public const string NavigationScoreOpened = "Navigation.Score.Opened";

    /// <summary>Front management navigation opened signal id.</summary>
    public const string NavigationFrontManageOpened = "Navigation.FrontManage.Opened";

    /// <summary>Smart BP navigation opened signal id.</summary>
    public const string NavigationSmartBpOpened = "Navigation.SmartBp.Opened";

    /// <summary>Map BP navigation opened signal id.</summary>
    public const string NavigationMapBpOpened = "Navigation.MapBp.Opened";

    /// <summary>Survivor ban navigation opened signal id.</summary>
    public const string NavigationBanSurvivorOpened = "Navigation.BanSurvivor.Opened";

    /// <summary>Hunter ban navigation opened signal id.</summary>
    public const string NavigationBanHunterOpened = "Navigation.BanHunter.Opened";

    /// <summary>Character pick navigation opened signal id.</summary>
    public const string NavigationPickCharacterOpened = "Navigation.PickCharacter.Opened";

    /// <summary>BP window opened signal id.</summary>
    public const string BpWindowOpened = "BpWindowOpened";

    /// <summary>BO1 first-half game progress selected signal id.</summary>
    public const string GameProgressSelectedBo1FirstHalf = "GameProgressSelected.Bo1FirstHalf";

    /// <summary>Team name confirmed signal id.</summary>
    public const string TeamNameConfirmed = "TeamNameConfirmed";

    /// <summary>Home team JSON imported signal id.</summary>
    public const string TeamJsonImportedHome = "TeamJsonImported.Home";

    /// <summary>Away team JSON imported signal id.</summary>
    public const string TeamJsonImportedAway = "TeamJsonImported.Away";

    /// <summary>Member state changed signal id.</summary>
    public const string MemberStateChanged = "MemberStateChanged";

    /// <summary>Member position swapped signal id.</summary>
    public const string MemberPositionSwapped = "MemberPositionSwapped";

    /// <summary>Game guidance started signal id.</summary>
    public const string GameGuidanceStarted = "GameGuidanceStarted";

    /// <summary>Guidance next clicked signal id.</summary>
    public const string GuidanceNextClicked = "GuidanceNextClicked";

    /// <summary>Guidance step changed signal id.</summary>
    public const string GuidanceStepChanged = "GuidanceStepChanged";

    /// <summary>Character selector search committed signal id.</summary>
    public const string CharacterSelectorSearchCommitted = "CharacterSelector.SearchCommitted";

    /// <summary>Character selector selection confirmed signal id.</summary>
    public const string CharacterSelectorSelectionConfirmed = "CharacterSelector.SelectionConfirmed";

    /// <summary>First survivor pick character selected signal id.</summary>
    public const string PickCharacterSelectedSurvivor1 = "PickCharacterSelected.Survivor1";

    /// <summary>Second survivor pick character selected signal id.</summary>
    public const string PickCharacterSelectedSurvivor2 = "PickCharacterSelected.Survivor2";

    /// <summary>Third survivor pick character selected signal id.</summary>
    public const string PickCharacterSelectedSurvivor3 = "PickCharacterSelected.Survivor3";

    /// <summary>Fourth survivor pick character selected signal id.</summary>
    public const string PickCharacterSelectedSurvivor4 = "PickCharacterSelected.Survivor4";

    /// <summary>All survivor pick slots completed signal id.</summary>
    public const string PickSurvivorSlotsCompleted = "PickSurvivorSlotsCompleted";

    /// <summary>Character changer applied signal id.</summary>
    public const string CharacterChangerApplied = "CharacterChangerApplied";

    /// <summary>Game guidance stopped signal id.</summary>
    public const string GameGuidanceStopped = "GameGuidanceStopped";

    /// <summary>Global ban record updated signal id.</summary>
    public const string GlobalBanRecordUpdated = "GlobalBanRecordUpdated";

    /// <summary>Score changed signal id.</summary>
    public const string ScoreChanged = "ScoreChanged";

    /// <summary>New game created signal id.</summary>
    public const string NewGameCreated = "NewGameCreated";

    /// <summary>Designer v3 opened signal id.</summary>
    public const string DesignerV3Opened = "DesignerV3.Opened";

    /// <summary>Front management open designer clicked signal id.</summary>
    public const string FrontManageOpenDesignerClicked = "FrontManage.OpenDesigner.Clicked";

    /// <summary>Smart BP module loaded signal id.</summary>
    public const string SmartBpModuleLoaded = "SmartBp.ModuleLoaded";

    /// <summary>next game button clicked signal id.</summary>
    public const string NextGameClicked = "GameGuidance.NextGameClicked";

    /// <summary>Animation editor animateProperty node selected signal id.</summary>
    public const string AnimationPropertyNodeSelected = "AnimationPropertyNodeSelected";
}

/// <summary>
/// Built-in tutorial target element names.
/// </summary>
public static class TutorialTargetNames
{
    /// <summary>Home team name input target name.</summary>
    public const string HomeTeamNameInput = "HomeTeamNameInput";

    /// <summary>Home team name confirm button target name.</summary>
    public const string HomeTeamNameConfirmButton = "HomeTeamNameConfirmButton";

    /// <summary>Home team logo button target name.</summary>
    public const string HomeTeamLogoButton = "HomeTeamLogoButton";

    /// <summary>Home team JSON import button target name.</summary>
    public const string HomeTeamJsonImportButton = "HomeTeamJsonImportButton";

    /// <summary>Home player list panel target name.</summary>
    public const string HomePlayerListPanel = "HomePlayerListPanel";

    /// <summary>Home player position panel target name.</summary>
    public const string HomePlayerPositionPanel = "HomePlayerPositionPanel";

    /// <summary>Away team JSON import button target name.</summary>
    public const string AwayTeamJsonImportButton = "AwayTeamJsonImportButton";

    /// <summary>Away player list panel target name.</summary>
    public const string AwayPlayerListPanel = "AwayPlayerListPanel";

    /// <summary>Next guidance step button target name.</summary>
    public const string NextGuidanceStepButton = "NextGuidanceStepButton";

    /// <summary>Survivor pick selector group border target name.</summary>
    public const string SurvivorPickSelectorGroupBorder = "SurvivorPickSelectorGroupBorder";

    /// <summary>First survivor pick selector host target name.</summary>
    public const string FirstSurvivorPickSelectorHost = "FirstSurvivorPickSelectorHost";

    /// <summary>Talent and trait selector panel target name.</summary>
    public const string TalentTraitSelectorPanel = "TalentTraitSelectorPanel";

    /// <summary>Survivor talent selector target name.</summary>
    public const string SurvivorTalentSelector = "SurvivorTalentSelector";

    /// <summary>Hunter trait selector target name.</summary>
    public const string HunterTraitSelector = "HunterTraitSelector";

    /// <summary>Global ban record panel target name.</summary>
    public const string GlobalBanRecordPanel = "GlobalBanRecordPanel";

    /// <summary>Map ban operation border target name.</summary>
    public const string MapBanOperationBorder = "MapBanOperationBorder";

    /// <summary>Map selector panel target name.</summary>
    public const string MapSelectorPanel = "MapSelectorPanel";

    /// <summary>Designer preview canvas target name.</summary>
    public const string PreviewCanvas = "PreviewCanvas";

    /// <summary>Designer interaction layer target name.</summary>
    public const string InteractionLayer = "InteractionLayer";

    /// <summary>Designer behavior panel host target name.</summary>
    public const string BehaviorPanelHost = "BehaviorPanelHost";

    /// <summary>Designer help button target name.</summary>
    public const string DesignerHelpButton = "DesignerHelpButton";

    /// <summary>Designer behavior help button target name.</summary>
    public const string BehaviorHelpButton = "BehaviorHelpButton";

    /// <summary>Designer animation editor graph canvas target name.</summary>
    public const string AnimationGraphCanvas = "GraphCanvas";

    /// <summary>Designer animation editor selected node property panel target name.</summary>
    public const string AnimationPropertyPanel = "AnimationPropertyPanel";

    /// <summary>Designer animation editor preview panel target name.</summary>
    public const string AnimationPreviewPanel = "PreviewBorder";

    /// <summary>Designer animation editor help button target name.</summary>
    public const string AnimationEditorHelpButton = "AnimationEditorHelpButton";

    /// <summary>Smart BP window selector target name.</summary>
    public const string SmartBpWindowSelector = "SmartBpWindowSelector";

    /// <summary>Smart BP start capture button target name.</summary>
    public const string SmartBpStartCaptureButton = "SmartBpStartCaptureButton";

    /// <summary>Smart BP preview button target name.</summary>
    public const string SmartBpPreviewButton = "SmartBpPreviewButton";

    /// <summary>Smart BP preview panel target name.</summary>
    public const string SmartBpPreviewPanel = "SmartBpPreviewPanel";

    /// <summary>Smart BP stop capture button target name.</summary>
    public const string SmartBpStopCaptureButton = "SmartBpStopCaptureButton";

    /// <summary>Smart BP region editor button target name.</summary>
    public const string SmartBpRegionEditorButton = "SmartBpRegionEditorButton";

    /// <summary>Smart BP region preview panel target name.</summary>
    public const string SmartBpRegionPreviewPanel = "SmartBpRegionPreviewPanel";

    /// <summary>Smart BP region list panel target name.</summary>
    public const string SmartBpRegionListPanel = "SmartBpRegionListPanel";

    /// <summary>Smart BP save region button target name.</summary>
    public const string SmartBpSaveRegionButton = "SmartBpSaveRegionButton";

    /// <summary>Smart BP start full BP flow button target name.</summary>
    public const string SmartBpStartFullBpFlowButton = "SmartBpStartFullBpFlowButton";
}
