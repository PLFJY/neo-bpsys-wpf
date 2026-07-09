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
                .Step("设置天赋和特质")
                    .Text("这里用于设置本局角色的天赋和特质。这些信息会用于前台展示和赛后记录。按比赛实际情况选择后继续引导。")
                    .TargetName(TutorialTargetNames.TalentTraitSelectorPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .AvatarPose(TutorialAvatarPose.RightBottom)
                    .CardOffset(new Point(-150, -24))
                    .Placement(ProductTourPlacement.TopRight)
                .Step("求生者天赋")
                    .Text("这里设置四名求生者的天赋。本教程不强制完整填写。")
                    .TargetName(TutorialTargetNames.SurvivorTalentSelector)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .CardOffset(new Point(-150, -100))
                    .AvatarPose(TutorialAvatarPose.RightBottom)
                .Step("监管者特质")
                    .Text("这里设置监管者天赋和辅助特质。确认实际比赛信息后继续对局引导。")
                    .TargetName(TutorialTargetNames.HunterTraitSelector)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .AvatarPose(TutorialAvatarPose.RightBottom)
                .Build();
    }
}
