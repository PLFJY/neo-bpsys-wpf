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
            TutorialPackages.GameManageBasic
        ]);

        registrar.RegisterSequence(TutorialPageKeys.BpGameGuidance,
        [
            TutorialPackageIds.BpGameGuidanceStartBasic,
            TutorialPackageIds.BpGameGuidanceCurrentStepBasic,
            TutorialPackageIds.MapBpBanMapOperationBasic,
            TutorialPackageIds.MapBpNextToPickMapBasic,
            TutorialPackageIds.BpGameGuidanceEndBasic,
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
        registrar.RegisterPackage(CreateGameGuidanceCurrentStepPackage());
        registrar.RegisterPackage(CreateMapBpBanMapOperationPackage());
        registrar.RegisterPackage(CreateMapBpNextToPickMapPackage(TutorialPackageIds.MapBpNextToPickMapBasic, 4));
        registrar.RegisterPackage(CreateGameGuidanceEndPackage());
        registrar.RegisterPackage(CreateGameGuidanceStartPackage(TutorialPackageIds.BpGameGuidanceBasic, 6));
        registrar.RegisterPackage(CreateMapBpNextToPickMapPackage(TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf, 7));
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

    private static TutorialPackageDefinition CreateMapBpNextToPickMapPackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKeys.BpGameGuidance,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(NextGuidanceStepButton),
                    "进入选择地图",
                    "Ban 地图完成后，点击下一步进入选择地图。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GuidanceNextClicked)
            ]);

    private static TutorialPackageDefinition CreateGameGuidanceCurrentStepPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackageIds.BpGameGuidanceCurrentStepBasic,
            TutorialPageKeys.BpGameGuidance,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(CurrentGuidanceStepTextBlock),
                    "查看当前步骤",
                    "这里会显示当前对局引导正在进行的步骤。每进入一个阶段，先看这里确认现在应该完成什么操作。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]);

    private static TutorialPackageDefinition CreateMapBpBanMapOperationPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackageIds.MapBpBanMapOperationBasic,
            TutorialPageKeys.BpGameGuidance,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.MapBanOperationBorder,
                    "Ban 地图操作区域",
                    "现在完成 Ban 地图。对局引导会高亮当前需要操作的区域。请在这里完成地图禁用。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.TopLeft,
                    avatarPose: TutorialAvatarPose.RightBottom,
                    cardOffset: new Point(0, -24),
                    placement: ProductTourPlacement.TopRight)
            ]);

    private static TutorialPackageDefinition CreateGameGuidanceEndPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackageIds.BpGameGuidanceEndBasic,
            TutorialPageKeys.BpGameGuidance,
            5,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(StopGameGuidanceButton),
                    "结束对局引导",
                    "当前对局引导已经完成。点击这里结束引导，之后可以进入比分页面记录结果。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameGuidanceStopped)
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

}
