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
                .Step("前台管理")
                    .Text("这里是前台管理页面。前台管理分为“前台窗口”和“布局包”两个区域。前台窗口负责打开、关闭观众看到的窗口。布局包负责导入、切换和管理前台界面方案。")
                    .TargetName(nameof(FrontManageTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }
}
