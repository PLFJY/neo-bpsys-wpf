using System.Windows;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Windows;

public partial class MainWindow
{
    /// <summary>Main window tutorial page key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.Main;

    /// <summary>Main window tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Main navigation basic package id.</summary>
        public const string NavigationBasic = TutorialPackageIds.MainNavigationBasic;

        /// <summary>Main navigation front management package id.</summary>
        public const string NavigationFrontManage = TutorialPackageIds.MainNavigationFrontManage;

        /// <summary>Main navigation team info package id.</summary>
        public const string NavigationTeamInfo = TutorialPackageIds.MainNavigationTeamInfo;

        /// <summary>Main navigation score package id.</summary>
        public const string NavigationScore = TutorialPackageIds.MainNavigationScore;

        /// <summary>Main navigation Smart BP package id.</summary>
        public const string NavigationSmartBp = TutorialPackageIds.MainNavigationSmartBp;

        /// <summary>Main navigation Designer v3 package id.</summary>
        public const string NavigationDesignerV3 = TutorialPackageIds.MainNavigationDesignerV3;

        /// <summary>Main team summary package id.</summary>
        public const string TeamSummaryBasic = TutorialPackageIds.MainTeamSummaryBasic;

        /// <summary>BO1 first-half game progress package id.</summary>
        public const string GameProgressBo1FirstHalf = TutorialPackageIds.GameManageGameProgressBo1FirstHalf;

        /// <summary>New game basic package id.</summary>
        public const string NewGameBasic = TutorialPackageIds.GameManageNewGameBasic;

        /// <summary>Global ban carry-over package id.</summary>
        public const string GlobalBanCarryOver = TutorialPackageIds.GameManageGlobalBanCarryOver;

        /// <summary>Game management basic package id.</summary>
        public const string GameManageBasic = TutorialPackageIds.GameManageBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the main window.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.NavigationFrontManage,
            TutorialPackages.NavigationTeamInfo,
            TutorialPackages.NavigationScore,
            TutorialPackages.NavigationSmartBp,
            TutorialPackages.NavigationDesignerV3,
            TutorialPackages.TeamSummaryBasic,
            TutorialPackages.NavigationBasic
        ]);

        registrar.RegisterSequence(TutorialPageKeys.GameManage,
        [
            TutorialPackages.GameProgressBo1FirstHalf,
            TutorialPackages.NewGameBasic,
            TutorialPackages.GlobalBanCarryOver,
            TutorialPackages.GameManageBasic,
            TutorialPackageIds.GameManageImportExport
        ]);

        registrar.RegisterSequence(TutorialPageKeys.BpGameGuidance,
        [
            TutorialPackageIds.BpGameGuidanceStartBasic,
            TutorialPackageIds.MapBpCompletionNextBasic,
            TutorialPackageIds.BpGameGuidanceBasic,
            TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf
        ]);

        registrar.RegisterPackage(CreateNavigationBasicPackage());
        registrar.RegisterPackage(CreateNavigationFrontManagePackage());
        registrar.RegisterPackage(CreateNavigationTeamInfoPackage());
        registrar.RegisterPackage(CreateNavigationScorePackage());
        registrar.RegisterPackage(CreateNavigationSmartBpPackage());
        registrar.RegisterPackage(CreateNavigationDesignerV3Package());
        registrar.RegisterPackage(CreateTeamSummaryPackage());
        registrar.RegisterPackage(CreateGameProgressPackage(TutorialPackages.GameProgressBo1FirstHalf, 1));
        registrar.RegisterPackage(CreateGameProgressPackage(TutorialPackages.GameManageBasic, 4));
        registrar.RegisterPackage(CreateNewGamePackage());
        registrar.RegisterPackage(CreateGlobalBanCarryOverPackage());
        registrar.RegisterPackage(CreateGameGuidanceStartPackage(TutorialPackageIds.BpGameGuidanceStartBasic, 1));
        registrar.RegisterPackage(CreateMapBpCompletionNextPackage(TutorialPackageIds.MapBpCompletionNextBasic, 2));
        registrar.RegisterPackage(CreateGameGuidanceStartPackage(TutorialPackageIds.BpGameGuidanceBasic, 3));
        registrar.RegisterPackage(CreateMapBpCompletionNextPackage(TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf, 4));
        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackageIds.GameManageImportExport,
            TutorialPageKeys.GameManage,
            5,
            [CreateFallbackStep(TutorialPackageIds.GameManageImportExport)]));
    }

    private static TutorialPackageDefinition CreateNavigationBasicPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NavigationBasic,
            TutorialPageKey,
            7,
            [
                TutorialDefinitionHelpers.NavigationStep(
                    typeof(TeamInfoPage).FullName!,
                    "进入队伍管理",
                    "先进入队伍管理页面，我们会设置本次教学使用的队伍。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationTeamInfoOpened)
            ]);

    private static TutorialPackageDefinition CreateNavigationFrontManagePackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NavigationFrontManage,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.NavigationStep(
                    typeof(FrontManagePage).FullName!,
                    "进入前台管理",
                    "先进入前台管理页面，打开 BP 前台窗口供 OBS 捕获。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationFrontManageOpened)
            ]);

    private static TutorialPackageDefinition CreateNavigationTeamInfoPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NavigationTeamInfo,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.NavigationStep(
                    typeof(TeamInfoPage).FullName!,
                    "进入队伍管理",
                    "进入队伍管理页面，设置教学使用的队伍信息。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationTeamInfoOpened)
            ]);

    private static TutorialPackageDefinition CreateNavigationScorePackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NavigationScore,
            TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.NavigationStep(
                    typeof(ScorePage).FullName!,
                    "进入比分页面",
                    "进入比分页面，选择当前半场的比分结果。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationScoreOpened)
            ]);

    private static TutorialPackageDefinition CreateNavigationSmartBpPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NavigationSmartBp,
            TutorialPageKey,
            4,
            [
                TutorialDefinitionHelpers.NavigationStep(
                    typeof(SmartBpPage).FullName!,
                    "进入智慧 BP",
                    "智慧 BP 是独立模块，首次进入后会有单独教程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationSmartBpOpened)
            ]);

    private static TutorialPackageDefinition CreateNavigationDesignerV3Package() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NavigationDesignerV3,
            TutorialPageKey,
            5,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "前台界面编辑",
                    "前台界面编辑、布局编辑和动画行为编辑，会在首次打开 v3 编辑器时单独教学。",
                    ProductTourInteractionMode.BlockAll)
            ]);

    private static TutorialPackageDefinition CreateGameGuidanceStartPackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKeys.BpGameGuidance,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(StartGameGuidanceButton),
                    "开启对局引导",
                    "对局引导会按照当前场次，带你完成地图、Ban/Pick 和后续流程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameGuidanceStarted)
            ]);

    private static TutorialPackageDefinition CreateMapBpCompletionNextPackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKeys.BpGameGuidance,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(NextGuidanceStepButton),
                    "进入下一阶段",
                    "当前阶段已经完成，点击下一步进入角色 BP。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GuidanceNextClicked)
            ]);

    private static TutorialPackageDefinition CreateTeamSummaryPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.TeamSummaryBasic,
            TutorialPageKey,
            6,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(TeamSummaryCard),
                    "确认队伍信息",
                    "队伍名已经显示在 MainWindow 上方功能区。这里也可以进行换边。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]);

    private static TutorialPackageDefinition CreateGameProgressPackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKeys.GameManage,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(GameProgressComboBox),
                    "选择场次",
                    "现在选择本次教学使用的场次。我们先从 BO1 上半开始。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
            ]);

    private static TutorialPackageDefinition CreateNewGamePackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.NewGameBasic,
            TutorialPageKeys.GameManage,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(NewGameButton),
                    "新建对局",
                    "新建对局会清空当前局的选择结果，但会保留全局禁选记录。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NewGameCreated)
            ]);

    private static TutorialPackageDefinition CreateGlobalBanCarryOverPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.GlobalBanCarryOver,
            TutorialPageKeys.GameManage,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "全局禁选继承",
                    "切换场次后，再次开启对局引导时，软件会自动导入之前的全局禁选记录，并设置对应 Ban 位。",
                    ProductTourInteractionMode.BlockAll)
            ]);

    private static ProductTourStep CreateFallbackStep(string packageId) =>
        TutorialDefinitionHelpers.Step(
            null,
            "功能教学",
            NeoBpsysTutorialTexts.GetFallbackDescription(packageId),
            ProductTourInteractionMode.BlockAll);
}
