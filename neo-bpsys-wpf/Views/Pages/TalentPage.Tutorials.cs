using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;

namespace neo_bpsys_wpf.Views.Pages;

public partial class TalentPage : ITutorialOwner<TalentPage>
{
    /// <summary>天赋与特质页面教程 Key。</summary>
    public const string TutorialPageKey = "Page.Bp.TalentTrait";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>天赋与特质教程包引用。</summary>
    public static class Tours
    {
        /// <summary>天赋与特质基础包引用。</summary>
        public static readonly TutorialPackageRef Basic = new(TutorialPackageIds.BpTalentTraitBasic);
    }

    /// <summary>
    /// 注册天赋与特质页面所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
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
