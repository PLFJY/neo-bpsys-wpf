using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
namespace neo_bpsys_wpf.Views.Pages;

public partial class FrontManagePage : ITutorialOwner<FrontManagePage>
{
    /// <summary>前台管理页面教程键。</summary>
    public const string TutorialPageKey = TutorialPageKeys.FrontManage;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>前台管理教程包引用。</summary>
    public static class Tours
    {
        /// <summary>前台管理概览包引用。</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.FrontManageOverview);
    }

    /// <summary>
    /// 注册前台管理页面拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
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
