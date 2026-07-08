using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

public partial class FrontedWindowsView
{
    /// <summary>Fronted windows view tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Front management BP window launch package id.</summary>
        public const string BpWindowLaunchBasic = TutorialPackageIds.FrontManageBpWindowLaunchBasic;

        /// <summary>Window management basic package id.</summary>
        public const string WindowsBasic = TutorialPackageIds.FrontManageWindowsBasic;

        /// <summary>Open Designer v3 package id.</summary>
        public const string OpenDesigner = TutorialPackageIds.FrontManageOpenDesigner;
    }

    /// <summary>Fronted windows view tutorial target names.</summary>
    public static class TutorialTargets
    {
        /// <summary>First manageable window card target name from the item template.</summary>
        public const string FirstManageableWindowCard = "FirstManageableWindowCard";
    }

    /// <summary>
    /// Registers tutorials owned by the fronted windows view.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.BpWindowLaunchBasic,
            FrontManagePage.TutorialPageKey,
            0,
            [
                TutorialDefinitionHelpers.ElementTagStep(
                    FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow),
                    "启动 BP 前台窗口",
                    "导播时，观众看到的是前台窗口。我们先只启动 BP 前台页面。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.BpWindowOpened)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.WindowsBasic,
            FrontManagePage.TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    "FrontedWindowsTab",
                    "前台窗口",
                    "这里管理所有前台窗口。导播排查和控制窗口时，可以在这里查看窗口列表。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(OpenFrontedDesignerButton),
                    "打开设计器",
                    "点击这里可以打开 v3 前台设计器，用来编辑前台布局、控件属性和动画行为。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(OpenAllFrontedWindowsButton),
                    "打开全部窗口",
                    "“打开全部”会一次性打开所有前台窗口，适合导播开播前检查 OBS 捕获来源。它可能同时弹出多个窗口，本教程不会要求你必须点击。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(CloseAllFrontedWindowsButton),
                    "关闭全部窗口",
                    "“关闭全部”会一次性关闭所有前台窗口，适合导播收尾或排查窗口状态。本教程不会等待关闭操作完成。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(StopAllLoopAnimationsButton),
                    "停止循环动画",
                    "如果前台循环动画异常，可以使用“停止所有循环动画”。这是导播排查动画状态时的控制功能。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(ManageableWindowGroupsPanel),
                    "窗口分组",
                    "这里按分组列出可管理的前台窗口。每个窗口都可以单独打开或关闭。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargets.FirstManageableWindowCard,
                    "单个窗口",
                    "单个窗口卡片可以独立打开或关闭对应前台窗口。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.OpenDesigner,
            FrontManagePage.TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(OpenFrontedDesignerButton),
                    "打开 v3 编辑器",
                    "v3 编辑器用于编辑前台布局、控件属性和动画行为。点击这里可以打开前台设计器。打开后会进入独立的 v3 编辑器教程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.DesignerV3Opened,
                    allowMissing: true)
            ]));
    }
}
