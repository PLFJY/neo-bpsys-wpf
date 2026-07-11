using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;

namespace neo_bpsys_wpf.Views.Pages;

public partial class PickPage : ITutorialOwner<PickPage>
{
    /// <summary>Character pick page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.Pick";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Character pick tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Pick character basic package reference.</summary>
        public static readonly TutorialPackageRef PickCharacterBasic = new(TutorialPackageIds.BpPickCharacterBasic);

        /// <summary>Global ban record package reference.</summary>
        public static readonly TutorialPackageRef GlobalBanRecordBasic = new(TutorialPackageIds.BpGlobalBanRecordBasic);

        /// <summary>Select four survivors package reference.</summary>
        public static readonly TutorialPackageRef SelectFourSurvivorsBasic = new(TutorialPackageIds.BpPickSelectFourSurvivorsBasic);

        /// <summary>Character changer package reference.</summary>
        public static readonly TutorialPackageRef CharacterChangerBasic = new(TutorialPackageIds.BpCharacterChangerBasic);
    }

    /// <summary>Pick page tutorial target names.</summary>
    public static class TutorialTargets
    {
        /// <summary>Current survivor team's global ban record panel target tag.</summary>
        public const string CurrentSurvivorGlobalBanRecordPanel = "CurrentSurvivorGlobalBanRecordPanel";
    }

    /// <summary>
    /// Registers tutorials owned by the pick page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<PickPage>()
            .Package(Tours.SelectFourSurvivorsBasic)
                .StepKey("Step.BpPickSelectFourSurvivorsBasic.0.Title")
                    .TextKey("Step.BpPickSelectFourSurvivorsBasic.0.Description")
                    .TargetName(nameof(SurvivorPickSelectorGroupBorder))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.TopLeft)
                    .WaitFor(TutorialSignalIds.PickSurvivorSlotsCompleted)
            .Package(Tours.GlobalBanRecordBasic)
                .StepKey("Step.BpGlobalBanRecordBasic.0.Title")
                    .TextKey("Step.BpGlobalBanRecordBasic.0.Description")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(GlobalBanRecordPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TargetName(nameof(GlobalBanRecordPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((_, _) =>
                    {
                        var gameGuidanceService = IAppHost.Host!.Services.GetRequiredService<IGameGuidanceService>();
                        gameGuidanceService.MoveToStepAsync(9);
                        return Task.CompletedTask;
                    })
            .Package(Tours.CharacterChangerBasic)
                .StepKey("Step.BpCharacterChangerBasic.0.Title")
                    .TextKey("Step.BpCharacterChangerBasic.0.Description")
                    .TargetName(nameof(SurvivorPickSelectorGroupBorder))
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SurvivorPickSelectorGroupBorder)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((_, _) =>
                    {
                        var gameGuidanceService = IAppHost.Host!.Services.GetRequiredService<IGameGuidanceService>();
                        gameGuidanceService.MoveToStepAsync(10);
                        return Task.CompletedTask;
                    })
                .Build();
    }
}
