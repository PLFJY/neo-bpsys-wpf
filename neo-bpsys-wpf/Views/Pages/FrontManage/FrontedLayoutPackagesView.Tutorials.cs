using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// Tutorial registration partial for <see cref="FrontedLayoutPackagesView"/>.
/// </summary>
public partial class FrontedLayoutPackagesView : ITutorialOwner<FrontedLayoutPackagesView>
{
    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Layout packages view tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Layout packages basic package reference.</summary>
        public static readonly TutorialPackageRef LayoutPackagesBasic = new(TutorialPackageIds.FrontManageLayoutPackagesBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the layout packages view.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForRegion<FrontedLayoutPackagesView>()
            .Package(Tours.LayoutPackagesBasic)
                .StepKey("Step.FrontManageLayoutPackagesBasic.0.Title")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .TextKey("Step.FrontManageLayoutPackagesBasic.0.Description")
                    .TargetName(nameof(LayoutPackageList))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageLayoutPackagesBasic.1.Title")
                    .TextKey("Step.FrontManageLayoutPackagesBasic.1.Description")
                    .TargetName(nameof(ActiveLayoutPackagePanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }
}
