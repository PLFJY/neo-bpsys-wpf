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

        /// <summary>NextGame Tutorial reference.</summary>
        public static readonly TutorialPackageRef NextGameBasic = new(TutorialPackageIds.NextGameBasic);

        /// <summary>Pick map Tutorial reference.</summary>
        public static readonly TutorialPackageRef MapBpPickMapBasic = new(TutorialPackageIds.MapBpPickMapOperationBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the main window.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
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
                    .TargetName(nameof(NewGameButton))
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
