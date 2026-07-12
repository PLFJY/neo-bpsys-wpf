using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// <see cref="FrontedLayoutPackagesView"/> 的教程注册分部类。
/// </summary>
public partial class FrontedLayoutPackagesView : ITutorialOwner<FrontedLayoutPackagesView>
{
    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>布局包视图教程包引用。</summary>
    public static class Tours
    {
        /// <summary>布局包基础包引用。</summary>
        public static readonly TutorialPackageRef LayoutPackagesBasic = new(TutorialPackageIds.FrontManageLayoutPackagesBasic);
    }

    /// <summary>
    /// 注册布局包视图所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
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
