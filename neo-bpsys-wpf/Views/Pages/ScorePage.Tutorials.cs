using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class ScorePage : ITutorialOwner<ScorePage>
{
    /// <summary>比分页面教程 Key。</summary>
    public const string TutorialPageKey = TutorialPageKeys.Score;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>比分页面教程包引用。</summary>
    public static class Tours
    {
        /// <summary>比分基础包引用。</summary>
        public static readonly TutorialPackageRef Basic = new(TutorialPackageIds.ScoreBasic);
    }

    /// <summary>
    /// 注册比分页面所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<ScorePage>()
            .Package(Tours.Basic)
                .StepKey("Step.ScoreBasic.0.Title")
                    .TextKey("Step.ScoreBasic.0.Description")
                    .TargetName(nameof(ScoreSelectorPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.ScoreChanged)
                .Build();
    }
}
