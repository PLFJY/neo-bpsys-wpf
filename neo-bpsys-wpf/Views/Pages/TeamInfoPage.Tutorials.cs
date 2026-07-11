using System.IO;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class TeamInfoPage : ITutorialOwner<TeamInfoPage>
{
    /// <summary>Team info page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.TeamInfo;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Team info tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Team info basic package reference.</summary>
        public static readonly TutorialPackageRef Basic = new(TutorialPackageIds.TeamInfoBasic);

        /// <summary>Team name basic package reference.</summary>
        public static readonly TutorialPackageRef TeamNameBasic = new(TutorialPackageIds.TeamInfoTeamNameBasic);

        /// <summary>Team JSON import package reference.</summary>
        public static readonly TutorialPackageRef JsonImport = new(TutorialPackageIds.TeamInfoJsonImport);

        /// <summary>Preset team JSON import package reference.</summary>
        public static readonly TutorialPackageRef JsonImportPreset = new(TutorialPackageIds.TeamInfoJsonImportPreset);

        /// <summary>Team player management package reference.</summary>
        public static readonly TutorialPackageRef PlayerManage = new(TutorialPackageIds.TeamInfoPlayerManage);

        /// <summary>Team info advanced package reference.</summary>
        public static readonly TutorialPackageRef Advanced = new(TutorialPackageIds.TeamInfoAdvanced);
    }

    /// <summary>
    /// Registers tutorials owned by the team info page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<TeamInfoPage>()
            .Package(Tours.TeamNameBasic)
                .StepKey("Step.TeamInfoTeamNameBasic.0.Title")
                    .TextKey("Step.TeamInfoTeamNameBasic.0.Description")
                    .TargetName(nameof(HomeTeamNameInput))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.TeamInfoTeamNameBasic.1.Title")
                    .TextKey("Step.TeamInfoTeamNameBasic.1.Description")
                    .TargetName(nameof(HomeTeamNameConfirmButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.TeamNameConfirmed)
                .StepKey("Step.TeamInfoTeamNameBasic.2.Title")
                    .TextKey("Step.TeamInfoTeamNameBasic.2.Description")
                    .TargetName(nameof(HomeTeamLogoButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.JsonImportPreset)
                .StepKey("Step.TeamInfoJsonImportPreset.0.Title")
                    .TextKey("Step.TeamInfoJsonImportPreset.0.Description")
                    .TargetName(nameof(HomeTeamJsonImportButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PreStepAction(SetExamplesJsonPickerHintAction("SetHomeTeamJsonPickerHint", "Hint.TeamInfoJsonImportPreset.HomePickerTitle"))
                    .WaitFor(TutorialSignalIds.TeamJsonImportedHome)
                .StepKey("Step.TeamInfoJsonImportPreset.1.Title")
                    .TextKey("Step.TeamInfoJsonImportPreset.1.Description")
                    .TargetName(nameof(HomePlayerListPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.BottomRight)
                    .AvatarPose(TutorialAvatarPose.LeftTop)
                .StepKey("Step.TeamInfoJsonImportPreset.2.Title")
                    .TextKey("Step.TeamInfoJsonImportPreset.2.Description")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(AwayTeamInfoCard)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(SetExamplesJsonPickerHintAction("SetAwayTeamJsonPickerHint", "Hint.TeamInfoJsonImportPreset.AwayPickerTitle"))
                    .TargetName(nameof(AwayTeamJsonImportButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.TeamJsonImportedAway)
                .StepKey("Step.TeamInfoJsonImportPreset.3.Title")
                    .TextKey("Step.TeamInfoJsonImportPreset.3.Description")
                    .TargetName(nameof(AwayPlayerListPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.BottomRight)
                    .AvatarPose(TutorialAvatarPose.LeftTop)
            .Package(Tours.PlayerManage)
                .StepKey("Step.TeamInfoPlayerManage.0.Title")
                    .TextKey("Step.TeamInfoPlayerManage.0.Description")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(HomePlayerPositionPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TargetName(nameof(HomePlayerPositionPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.BottomRight)
                    .AvatarPose(TutorialAvatarPose.LeftTop)
                .Build();
    }

    private static TutorialStepAction SetExamplesJsonPickerHintAction(string name, string titleKey) =>
        new(name, (_, _) =>
        {
            var title = I18nHelper.GetLocalizedString(AppI18nDictionaries.TourContent, titleKey);
            SetExamplesJsonPickerHint(title);
            return Task.CompletedTask;
        });

    private static void SetExamplesJsonPickerHint(string title)
    {
        TutorialFilePickerHints.SetNextJsonPickerHint(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples"),
            title);
    }
}
