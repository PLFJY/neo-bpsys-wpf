using System.Windows;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Windows;

public partial class MainWindow : ITutorialOwner<MainWindow>
{
    /// <summary>Main window tutorial page key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.Main;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Main window tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Main navigation basic package reference.</summary>
        public static readonly TutorialPackageRef NavigationBasic = new(TutorialPackageIds.MainNavigationBasic);

        /// <summary>Main navigation front management package reference.</summary>
        public static readonly TutorialPackageRef NavigationFrontManage = new(TutorialPackageIds.MainNavigationFrontManage);

        /// <summary>Main navigation team info package reference.</summary>
        public static readonly TutorialPackageRef NavigationTeamInfo = new(TutorialPackageIds.MainNavigationTeamInfo);

        /// <summary>Main navigation score package reference.</summary>
        public static readonly TutorialPackageRef NavigationScore = new(TutorialPackageIds.MainNavigationScore);

        /// <summary>Main navigation Smart BP package reference.</summary>
        public static readonly TutorialPackageRef NavigationSmartBp = new(TutorialPackageIds.MainNavigationSmartBp);

        /// <summary>Main navigation Designer v3 package reference.</summary>
        public static readonly TutorialPackageRef NavigationDesignerV3 = new(TutorialPackageIds.MainNavigationDesignerV3);

        /// <summary>Main team summary package reference.</summary>
        public static readonly TutorialPackageRef TeamSummaryBasic = new(TutorialPackageIds.MainTeamSummaryBasic);

        /// <summary>BO1 first-half game progress package reference.</summary>
        public static readonly TutorialPackageRef GameProgressBo1FirstHalf = new(TutorialPackageIds.GameManageGameProgressBo1FirstHalf);

        /// <summary>New game basic package reference.</summary>
        public static readonly TutorialPackageRef NewGameBasic = new(TutorialPackageIds.GameManageNewGameBasic);

        /// <summary>Global ban carry-over package reference.</summary>
        public static readonly TutorialPackageRef GlobalBanCarryOver = new(TutorialPackageIds.GameManageGlobalBanCarryOver);

        /// <summary>Game management basic package reference.</summary>
        public static readonly TutorialPackageRef GameManageBasic = new(TutorialPackageIds.GameManageBasic);

        /// <summary>BP guidance start package reference.</summary>
        public static readonly TutorialPackageRef BpGameGuidanceStartBasic = new(TutorialPackageIds.BpGameGuidanceStartBasic);

        /// <summary>BP guidance current step package reference.</summary>
        public static readonly TutorialPackageRef BpGameGuidanceCurrentStepBasic = new(TutorialPackageIds.BpGameGuidanceCurrentStepBasic);

        /// <summary>Map BP ban map operation package reference.</summary>
        public static readonly TutorialPackageRef MapBpBanMapOperationBasic = new(TutorialPackageIds.MapBpBanMapOperationBasic);

        /// <summary>Map BP next-to-pick-map package reference.</summary>
        public static readonly TutorialPackageRef MapBpNextToPickMapBasic = new(TutorialPackageIds.MapBpNextToPickMapBasic);

        /// <summary>BP guidance end package reference.</summary>
        public static readonly TutorialPackageRef BpGameGuidanceEndBasic = new(TutorialPackageIds.BpGameGuidanceEndBasic);

        /// <summary>BP guidance basic package reference.</summary>
        public static readonly TutorialPackageRef BpGameGuidanceBasic = new(TutorialPackageIds.BpGameGuidanceBasic);

        /// <summary>BO1 first-half BP guidance flow package reference.</summary>
        public static readonly TutorialPackageRef BpGameGuidanceFlowBo1FirstHalf = new(TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf);

        /// <summary>NextGame Tutorial reference.</summary>
        public static readonly TutorialPackageRef NextGameBasic = new(TutorialPackageIds.NextGameBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the main window.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<MainWindow>()
            .Package(Tours.NavigationFrontManage)
                .Step("进入前台管理")
                    .Text("先进入前台管理页面，打开 BP 前台窗口供 OBS 捕获。")
                    .TargetNavigation<FrontManagePage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationFrontManageOpened)
            .Package(Tours.NavigationTeamInfo)
                .Step("进入队伍管理")
                    .Text("进入队伍管理页面，设置教学使用的队伍信息。")
                    .TargetNavigation<TeamInfoPage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationTeamInfoOpened)
            .Package(Tours.NavigationScore)
                .Step("进入比分页面")
                    .Text("进入比分页面，选择当前半场的比分结果。")
                    .TargetNavigation<ScorePage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationScoreOpened)
            .Package(Tours.NavigationSmartBp)
                .Step("进入智慧 BP")
                    .Text("智慧 BP 是独立模块，首次进入后会有单独教程。")
                    .TargetNavigation<SmartBpPage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationSmartBpOpened)
            .Package(Tours.NavigationDesignerV3)
                .Step("前台界面编辑")
                    .Text("前台界面编辑、布局编辑和动画行为编辑，会在首次打开 v3 编辑器时单独教学。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
            .Package(Tours.TeamSummaryBasic)
                .Step("确认队伍信息")
                    .Text("队伍名已经显示在 MainWindow 上方功能区。这里也可以进行换边。")
                    .TargetName(nameof(TeamSummaryCard))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.NavigationBasic)
                .Step("进入队伍管理")
                    .Text("先进入队伍管理页面，我们会设置本次教学使用的队伍。")
                    .TargetNavigation<TeamInfoPage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationTeamInfoOpened)
            .Build();

        builder.ForKey<MainWindow>(TutorialPageKeys.GameManage)
            .Package(Tours.GameProgressBo1FirstHalf)
                .Step("选择场次")
                    .Text("现在选择本次教学使用的场次。我们先从 BO1 上半开始。")
                    .TargetName(nameof(GameProgressComboBox))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
            .Package(Tours.NewGameBasic)
                .Step("新建对局")
                    .Text("新建对局会清空当前局的选择结果，但会保留全局禁选记录。")
                    .TargetName(nameof(NewGameButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.NewGameCreated)
            .Package(Tours.GlobalBanCarryOver)
                .Step("全局禁选继承")
                    .Text("切换场次后，再次开启对局引导时，软件会自动导入之前的全局禁选记录，并设置对应 Ban 位。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
            .Package(Tours.GameManageBasic)
                .Step("确认场次")
                    .Text("现在选择本次教学使用的场次。我们先从 BO1 上半开始。")
                    .TargetName(nameof(GameProgressComboBox))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
                .Build();

        builder.ForKey<MainWindow>(TutorialPageKeys.BpGameGuidance)
            .Package(Tours.BpGameGuidanceStartBasic)
                .Step("开启对局引导")
                    .Text("对局引导会按照当前场次，带你完成地图、Ban/Pick 和后续流程。")
                    .TargetName(nameof(StartGameGuidanceButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameGuidanceStarted)
            .Package(Tours.BpGameGuidanceCurrentStepBasic)
                .Step("查看当前步骤")
                    .Text("这里会显示当前对局引导正在进行的步骤。每进入一个阶段，先看这里确认现在应该完成什么操作。")
                    .TargetName(nameof(CurrentGuidanceStepTextBlock))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.MapBpBanMapOperationBasic)
                .Step("Ban 地图操作区域")
                    .Text("现在完成 Ban 地图。对局引导会高亮当前需要操作的区域。请在这里完成地图禁用。")
                    .TargetName(TutorialTargetNames.MapBanOperationBorder)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .Placement(ProductTourPlacement.TopRight)
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .AvatarPose(TutorialAvatarPose.RightBottom)
                    .CardOffset(new Point(-100, -24))
            .Package(Tours.MapBpNextToPickMapBasic)
                .Step("进入选择地图")
                    .Text("Ban 地图完成后，点击下一步进入选择地图。")
                    .TargetName(nameof(NextGuidanceStepButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GuidanceNextClicked)
            .Package(Tours.BpGameGuidanceEndBasic)
                .Step("结束对局引导")
                    .Text("当前对局引导已经完成。点击这里结束引导，之后可以进入比分页面记录结果。")
                    .TargetName(nameof(StopGameGuidanceButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameGuidanceStopped)
            .Package(Tours.BpGameGuidanceBasic)
                .Step("继续对局引导")
                    .Text("对局引导会按照当前场次，带你完成地图、Ban/Pick 和后续流程。")
                    .TargetName(nameof(StartGameGuidanceButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameGuidanceStarted)
            .Package(Tours.BpGameGuidanceFlowBo1FirstHalf)
                .Step("进入地图选择流程")
                    .Text("Ban 地图完成后，点击下一步进入选择地图。")
                    .TargetName(nameof(NextGuidanceStepButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GuidanceNextClicked)
            .Package(Tours.NextGameBasic)
                .Step("点击进入下一局")
                    .Text("当前对局结束后点击将场次切换到下一局")
                    .TargetName(nameof(NextGameButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .WaitFor(TutorialSignalIds.NextGameClicked)
            .Build();
    }

    private static TutorialStepAction CompleteNavigationStepAction() =>
        new("CompleteNavigationStep", (_, ct) => Task.Delay(250, ct));
}
