using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class ScorePage
{
    /// <summary>Score page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.Score;

    /// <summary>Score page tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Score basic package id.</summary>
        public const string Basic = TutorialPackageIds.ScoreBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the score page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.Basic
        ]);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Basic,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(ScoreSelectorPanel),
                    "选择比分",
                    "对局结束后，可以在这里选择比分。比分会同步到比赛状态和前台显示。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.ScoreChanged,
                    allowMissing: true)
            ]));
    }
}
