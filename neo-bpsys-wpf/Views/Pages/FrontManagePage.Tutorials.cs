using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
namespace neo_bpsys_wpf.Views.Pages;

public partial class FrontManagePage : ITutorialOwner<FrontManagePage>
{
    /// <summary>Front management page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.FrontManage;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Front management tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Front management overview package reference.</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.FrontManageOverview);
    }

    /// <summary>
    /// Registers tutorials owned by the front management page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<FrontManagePage>()
            .Package(Tours.Overview)
                .StepKey("Step.FrontManageOverview.0.Title")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .TextKey("Step.FrontManageOverview.0.Description")
                    .TargetName(nameof(FrontManageTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }
}
