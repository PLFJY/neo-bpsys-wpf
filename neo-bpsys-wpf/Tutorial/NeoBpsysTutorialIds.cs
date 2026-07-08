namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Built-in tutorial flow ids.
/// </summary>
public static class TutorialFlowIds
{
    /// <summary>The standard first-run BP tutorial flow id.</summary>
    public const string FirstRunStandardBp = "Flow.FirstRun.StandardBp";

    /// <summary>Phase 4A navigation probe tutorial flow id.</summary>
    public const string Phase4ANavigationProbe = "Flow.Phase4A.NavigationProbe";
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

    /// <summary>Shared BP page key.</summary>
    public const string BpShared = "Page.Bp.Shared";

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

    /// <summary>Team info basic package id.</summary>
    public const string TeamInfoBasic = "Page.TeamInfo.Basic";

    /// <summary>Team JSON import package id.</summary>
    public const string TeamInfoJsonImport = "Page.TeamInfo.JsonImport";

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

    /// <summary>BO1 first-half game guidance package id.</summary>
    public const string BpGameGuidanceFlowBo1FirstHalf = "Page.Bp.GameGuidance.FlowBo1FirstHalf";

    /// <summary>Score basic package id.</summary>
    public const string ScoreBasic = "Page.Score.Basic";

    /// <summary>Score fronted sync package id.</summary>
    public const string ScoreFrontedSync = "Page.Score.FrontedSync";

    /// <summary>Score advanced package id.</summary>
    public const string ScoreAdvanced = "Page.Score.Advanced";

    /// <summary>Game management basic package id.</summary>
    public const string GameManageBasic = "Page.GameManage.Basic";

    /// <summary>Game management import/export package id.</summary>
    public const string GameManageImportExport = "Page.GameManage.ImportExport";

    /// <summary>Global ban carry-over package id.</summary>
    public const string GameManageGlobalBanCarryOver = "Page.GameManage.GlobalBanCarryOver";

    /// <summary>Front management BP window launch package id.</summary>
    public const string FrontManageBpWindowLaunchBasic = "Page.FrontManage.BpWindowLaunch.Basic";

    /// <summary>Front management windows package id.</summary>
    public const string FrontManageWindowsBasic = "Page.FrontManage.Windows.Basic";

    /// <summary>Front management layout packages package id.</summary>
    public const string FrontManageLayoutPackagesBasic = "Page.FrontManage.LayoutPackages.Basic";

    /// <summary>Front management advanced package id.</summary>
    public const string FrontManageAdvanced = "Page.FrontManage.Advanced";

    /// <summary>Designer v3 layout edit package id.</summary>
    public const string DesignerV3LayoutEditBasic = "Window.DesignerV3.LayoutEdit.Basic";

    /// <summary>Designer v3 behavior edit package id.</summary>
    public const string DesignerV3BehaviorEditBasic = "Window.DesignerV3.BehaviorEdit.Basic";

    /// <summary>Designer v3 package import/export package id.</summary>
    public const string DesignerV3PackageImportExport = "Window.DesignerV3.PackageImportExport";

    /// <summary>Designer v3 advanced package id.</summary>
    public const string DesignerV3Advanced = "Window.DesignerV3.Advanced";

    /// <summary>Smart BP module shell package id.</summary>
    public const string SmartBpModuleShell = "Page.SmartBp.ModuleShell";

    /// <summary>Smart BP capture package id.</summary>
    public const string SmartBpCaptureBasic = "Page.SmartBp.Capture.Basic";

    /// <summary>Smart BP region editor package id.</summary>
    public const string SmartBpRegionEditorBasic = "Page.SmartBp.RegionEditor.Basic";

    /// <summary>Smart BP full BP flow package id.</summary>
    public const string SmartBpFullBpFlowBasic = "Page.SmartBp.FullBpFlow.Basic";

    /// <summary>Smart BP post-game auto-fill package id.</summary>
    public const string SmartBpPostGameAutoFill = "Page.SmartBp.PostGameAutoFill";
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

    /// <summary>Map BP completed signal id.</summary>
    public const string MapBpCompleted = "MapBpCompleted";

    /// <summary>Guidance next clicked signal id.</summary>
    public const string GuidanceNextClicked = "GuidanceNextClicked";

    /// <summary>Guidance step changed signal id.</summary>
    public const string GuidanceStepChanged = "GuidanceStepChanged";

    /// <summary>Character selector search committed signal id.</summary>
    public const string CharacterSelectorSearchCommitted = "CharacterSelector.SearchCommitted";

    /// <summary>Character selector selection confirmed signal id.</summary>
    public const string CharacterSelectorSelectionConfirmed = "CharacterSelector.SelectionConfirmed";

    /// <summary>Global ban record updated signal id.</summary>
    public const string GlobalBanRecordUpdated = "GlobalBanRecordUpdated";

    /// <summary>Score changed signal id.</summary>
    public const string ScoreChanged = "ScoreChanged";

    /// <summary>New game created signal id.</summary>
    public const string NewGameCreated = "NewGameCreated";
}

/// <summary>
/// Built-in tutorial target element names.
/// </summary>
public static class TutorialTargetNames
{
    /// <summary>BP window launch button target name.</summary>
    public const string BpWindowLaunchButton = "BpWindowLaunchButton";

    /// <summary>Game progress combo box target name.</summary>
    public const string GameProgressComboBox = "GameProgressComboBox";

    /// <summary>Team name input target name.</summary>
    public const string TeamNameInput = "TeamNameInput";

    /// <summary>Team summary card target name.</summary>
    public const string TeamSummaryCard = "TeamSummaryCard";

    /// <summary>Import team JSON button target name.</summary>
    public const string ImportTeamJsonButton = "ImportTeamJsonButton";

    /// <summary>Player list target name.</summary>
    public const string PlayerList = "PlayerList";

    /// <summary>Player position panel target name.</summary>
    public const string PlayerPositionPanel = "PlayerPositionPanel";

    /// <summary>Stop Smart BP automatic recognition button target name.</summary>
    public const string StopSmartBpAutoRecognitionButton = "StopSmartBpAutoRecognitionButton";

    /// <summary>Start game guidance button target name.</summary>
    public const string StartGameGuidanceButton = "StartGameGuidanceButton";

    /// <summary>Previous guidance step button target name.</summary>
    public const string PreviousGuidanceStepButton = "PreviousGuidanceStepButton";

    /// <summary>Next guidance step button target name.</summary>
    public const string NextGuidanceStepButton = "NextGuidanceStepButton";

    /// <summary>Stop game guidance button target name.</summary>
    public const string StopGameGuidanceButton = "StopGameGuidanceButton";

    /// <summary>Legacy incorrect start guidance target name.</summary>
    [Obsolete("Use StopSmartBpAutoRecognitionButton for the Smart BP stop button or StartGameGuidanceButton for the game guidance start button.")]
    public const string LegacyIncorrectStartGameGuidanceButton = "StartGameGuidanceButton";

    /// <summary>Legacy incorrect previous guidance target name.</summary>
    [Obsolete("Use StartGameGuidanceButton.")]
    public const string LegacyIncorrectPreviousGuidanceStepButton = "PreviousGuidanceStepButton";

    /// <summary>Legacy incorrect next guidance target name.</summary>
    [Obsolete("Use PreviousGuidanceStepButton.")]
    public const string LegacyIncorrectNextGuidanceStepButton = "NextGuidanceStepButton";

    /// <summary>Character selector target name.</summary>
    public const string CharacterSelector = "CharacterSelector";

    /// <summary>Global ban record panel target name.</summary>
    public const string GlobalBanRecordPanel = "GlobalBanRecordPanel";

    /// <summary>Score selector panel target name.</summary>
    public const string ScoreSelectorPanel = "ScoreSelectorPanel";

    /// <summary>New game button target name.</summary>
    public const string NewGameButton = "NewGameButton";
}
