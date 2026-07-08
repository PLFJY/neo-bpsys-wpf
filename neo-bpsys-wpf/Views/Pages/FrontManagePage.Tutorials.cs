using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages.FrontManage;

namespace neo_bpsys_wpf.Views.Pages;

public partial class FrontManagePage
{
    /// <summary>Front management page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.FrontManage;

    /// <summary>Front management tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Front management overview package id.</summary>
        public const string Overview = TutorialPackageIds.FrontManageOverview;
    }

    /// <summary>
    /// Registers tutorials owned by the front management page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.Overview,
            FrontedWindowsView.TutorialPackages.WindowsBasic,
            FrontedWindowsView.TutorialPackages.OpenDesigner,
            FrontedLayoutPackagesView.TutorialPackages.LayoutPackagesBasic
        ]);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Overview,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(FrontManageTabs),
                    "前台管理",
                    "这里是前台管理页面。前台管理分为“前台窗口”和“布局包”两个区域。前台窗口负责打开、关闭观众看到的窗口。布局包负责导入、切换和管理前台界面方案。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));
    }
}
