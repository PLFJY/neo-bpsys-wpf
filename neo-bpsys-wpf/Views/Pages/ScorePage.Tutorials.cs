using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class ScorePage : ITutorialOwner<ScorePage>
{
    /// <summary>Score page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.Score;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Score page tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Score basic package reference.</summary>
        public static readonly TutorialPackageRef Basic = new(TutorialPackageIds.ScoreBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the score page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<ScorePage>()
            .Package(Tours.Basic)
                .Step("选择比分")
                    .Text("对局结束后，可以在这里选择比分。比分会同步到比赛状态和前台显示。")
                    .TargetName(nameof(ScoreSelectorPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.ScoreChanged)
                .Build();
    }
}
