using System.Reflection.Metadata.Ecma335;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Windows;

public partial class MainWindow : ITutorialOwner<MainWindow>
{
    /// <summary>主窗口教程页面 Key。</summary>
    public const string TutorialPageKey = TutorialPageKeys.Main;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>主窗口教程包引用。</summary>
    public static class Tours
    {
        /// <summary>主导航基础包引用。</summary>
        public static readonly TutorialPackageRef NavigationBasic = new(TutorialPackageIds.MainNavigationBasic);

        /// <summary>主导航前台管理包引用。</summary>
        public static readonly TutorialPackageRef NavigationFrontManage = new(TutorialPackageIds.MainNavigationFrontManage);

        /// <summary>主导航队伍信息包引用。</summary>
        public static readonly TutorialPackageRef NavigationTeamInfo = new(TutorialPackageIds.MainNavigationTeamInfo);

        /// <summary>主导航比分包引用。</summary>
        public static readonly TutorialPackageRef NavigationScore = new(TutorialPackageIds.MainNavigationScore);

        /// <summary>主导航 Smart BP 包引用。</summary>
        public static readonly TutorialPackageRef NavigationSmartBp = new(TutorialPackageIds.MainNavigationSmartBp);

        /// <summary>主导航设计器 v3 包引用。</summary>
        public static readonly TutorialPackageRef NavigationDesignerV3 = new(TutorialPackageIds.MainNavigationDesignerV3);

        /// <summary>主队伍摘要包引用。</summary>
        public static readonly TutorialPackageRef TeamSummaryBasic = new(TutorialPackageIds.MainTeamSummaryBasic);

        /// <summary>BO1 上半场对局进度包引用。</summary>
        public static readonly TutorialPackageRef GameProgressBo1FirstHalf = new(TutorialPackageIds.GameManageGameProgressBo1FirstHalf);

        /// <summary>新建对局基础包引用。</summary>
        public static readonly TutorialPackageRef NewGameBasic = new(TutorialPackageIds.GameManageNewGameBasic);

        /// <summary>全局 Ban 结转包引用。</summary>
        public static readonly TutorialPackageRef GlobalBanCarryOver = new(TutorialPackageIds.GameManageGlobalBanCarryOver);

        /// <summary>对局管理基础包引用。</summary>
        public static readonly TutorialPackageRef GameManageBasic = new(TutorialPackageIds.GameManageBasic);

        /// <summary>BP 引导开始包引用。</summary>
        public static readonly TutorialPackageRef BpGameGuidanceStartBasic = new(TutorialPackageIds.BpGameGuidanceStartBasic);

        /// <summary>BP 引导当前步骤包引用。</summary>
        public static readonly TutorialPackageRef BpGameGuidanceCurrentStepBasic = new(TutorialPackageIds.BpGameGuidanceCurrentStepBasic);

        /// <summary>地图 BP Ban 地图操作包引用。</summary>
        public static readonly TutorialPackageRef MapBpBanMapOperationBasic = new(TutorialPackageIds.MapBpBanMapOperationBasic);

        /// <summary>地图 BP 下一个 Pick 地图包引用。</summary>
        public static readonly TutorialPackageRef MapBpNextToPickMapBasic = new(TutorialPackageIds.MapBpNextToPickMapBasic);

        /// <summary>BP 引导结束包引用。</summary>
        public static readonly TutorialPackageRef BpGameGuidanceEndBasic = new(TutorialPackageIds.BpGameGuidanceEndBasic);

        /// <summary>BP 引导基础包引用。</summary>
        public static readonly TutorialPackageRef BpGameGuidanceBasic = new(TutorialPackageIds.BpGameGuidanceBasic);

        /// <summary>下一局教程引用。</summary>
        public static readonly TutorialPackageRef NextGameBasic = new(TutorialPackageIds.NextGameBasic);

        /// <summary>Pick 地图教程引用。</summary>
        public static readonly TutorialPackageRef MapBpPickMapBasic = new(TutorialPackageIds.MapBpPickMapOperationBasic);
    }

    /// <summary>
    /// 注册主窗口所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<MainWindow>()
            .Package(Tours.NavigationFrontManage)
                .StepKey("Step.MainNavigationFrontManage.0.Title")
                    .TextKey("Step.MainNavigationFrontManage.0.Description")
                    .TargetNavigation<FrontManagePage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationFrontManageOpened)
            .Package(Tours.NavigationTeamInfo)
                .StepKey("Step.MainNavigationTeamInfo.0.Title")
                    .TextKey("Step.MainNavigationTeamInfo.0.Description")
                    .TargetNavigation<TeamInfoPage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationTeamInfoOpened)
            .Package(Tours.NavigationScore)
                .StepKey("Step.MainNavigationScore.0.Title")
                    .TextKey("Step.MainNavigationScore.0.Description")
                    .TargetNavigation<ScorePage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationScoreOpened)
            .Package(Tours.NavigationSmartBp)
                .StepKey("Step.MainNavigationSmartBp.0.Title")
                    .TextKey("Step.MainNavigationSmartBp.0.Description")
                    .TargetNavigation<SmartBpPage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationSmartBpOpened)
            .Package(Tours.NavigationDesignerV3)
                .StepKey("Step.MainNavigationDesignerV3.0.Title")
                    .TextKey("Step.MainNavigationDesignerV3.0.Description")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
            .Package(Tours.TeamSummaryBasic)
                .StepKey("Step.MainTeamSummaryBasic.0.Title")
                    .TextKey("Step.MainTeamSummaryBasic.0.Description")
                    .TargetName(nameof(TeamSummaryCard))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.NavigationBasic)
                .StepKey("Step.MainNavigationBasic.0.Title")
                    .TextKey("Step.MainNavigationBasic.0.Description")
                    .TargetNavigation<TeamInfoPage>()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .PostStepAction(CompleteNavigationStepAction())
                    .WaitFor(TutorialSignalIds.NavigationTeamInfoOpened)
            .Build();

        builder.ForKey<MainWindow>(TutorialPageKeys.GameManage)
            .Package(Tours.GameProgressBo1FirstHalf)
                .StepKey("Step.GameManageGameProgressBo1FirstHalf.0.Title")
                    .TextKey("Step.GameManageGameProgressBo1FirstHalf.0.Description")
                    .TargetName(nameof(GameProgressComboBox))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
            .Package(Tours.NewGameBasic)
                .StepKey("Step.GameManageNewGameBasic.0.Title")
                    .TextKey("Step.GameManageNewGameBasic.0.Description")
                    .TargetName(nameof(NextGameButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.NewGameCreated)
            .Package(Tours.GlobalBanCarryOver)
                .StepKey("Step.GameManageGlobalBanCarryOver.0.Title")
                    .TextKey("Step.GameManageGlobalBanCarryOver.0.Description")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
            .Package(Tours.GameManageBasic)
                .StepKey("Step.GameManageBasic.0.Title")
                    .TextKey("Step.GameManageBasic.0.Description")
                    .TargetName(nameof(GameProgressComboBox))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
                .Build();

        builder.ForKey<MainWindow>(TutorialPageKeys.BpGameGuidance)
            .Package(Tours.BpGameGuidanceStartBasic)
                .StepKey("Step.BpGameGuidanceStartBasic.0.Title")
                    .TextKey("Step.BpGameGuidanceStartBasic.0.Description")
                    .TargetName(nameof(StartGameGuidanceButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameGuidanceStarted)
            .Package(Tours.BpGameGuidanceCurrentStepBasic)
                .StepKey("Step.BpGameGuidanceCurrentStepBasic.0.Title")
                    .TextKey("Step.BpGameGuidanceCurrentStepBasic.0.Description")
                    .TargetName(nameof(CurrentGuidanceStepTextBlock))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.MapBpBanMapOperationBasic)
                .StepKey("Step.MapBpBanMapOperationBasic.0.Title")
                    .TextKey("Step.MapBpBanMapOperationBasic.0.Description")
                    .TargetName(TutorialTargetNames.MapBanOperationBorder)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.MapBpNextToPickMapBasic)
                .StepKey("Step.MapBpNextToPickMapBasic.0.Title")
                    .TextKey("Step.MapBpNextToPickMapBasic.0.Description")
                    .TargetName(nameof(NextGuidanceStepButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GuidanceNextClicked)
            .Package(Tours.MapBpPickMapBasic)
                .StepKey("Step.MapBpPickMapOperationBasic.0.Title")
                    .TextKey("Step.MapBpPickMapOperationBasic.0.Description")
                    .TargetName(TutorialTargetNames.MapSelectorPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((_, _) =>
                    {
                        IAppHost.Host!.Services.GetRequiredService<IGameGuidanceService>().MoveToStepAsync(3);
                        return Task.CompletedTask;
                    })
            .Package(Tours.BpGameGuidanceEndBasic)
                .StepKey("Step.BpGameGuidanceEndBasic.0.Title")
                    .TextKey("Step.BpGameGuidanceEndBasic.0.Description")
                    .TargetName(nameof(StopGameGuidanceButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameGuidanceStopped)
            .Package(Tours.BpGameGuidanceBasic)
                .StepKey("Step.BpGameGuidanceBasic.0.Title")
                    .TextKey("Step.BpGameGuidanceBasic.0.Description")
                    .TargetName(nameof(StartGameGuidanceButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.GameGuidanceStarted)
            .Package(Tours.NextGameBasic)
                .StepKey("Step.NextGameBasic.0.Title")
                    .TextKey("Step.NextGameBasic.0.Description")
                    .TargetName(nameof(NextGameButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .WaitFor(TutorialSignalIds.NextGameClicked)
            .Build();
    }

    private static TutorialStepAction CompleteNavigationStepAction() =>
        new("CompleteNavigationStep", (_, ct) => Task.Delay(250, ct));
}
