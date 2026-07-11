using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;

namespace neo_bpsys_wpf.Views.Pages;

public partial class TalentPage : ITutorialOwner<TalentPage>
{
    /// <summary>Talent and trait page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.TalentTrait";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Talent and trait tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Talent and trait basic package reference.</summary>
        public static readonly TutorialPackageRef Basic = new(TutorialPackageIds.BpTalentTraitBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the talent and trait page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<TalentPage>()
            .Package(Tours.Basic)
                .StepKey("Step.BpTalentTraitBasic.0.Title")
                    .TextKey("Step.BpTalentTraitBasic.0.Description")
                    .TargetName(TutorialTargetNames.TalentTraitSelectorPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.BpTalentTraitBasic.1.Title")
                    .TextKey("Step.BpTalentTraitBasic.1.Description")
                    .TargetName(TutorialTargetNames.SurvivorTalentSelector)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.BpTalentTraitBasic.2.Title")
                    .TextKey("Step.BpTalentTraitBasic.2.Description")
                    .TargetName(TutorialTargetNames.HunterTraitSelector)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Build();
    }
}
