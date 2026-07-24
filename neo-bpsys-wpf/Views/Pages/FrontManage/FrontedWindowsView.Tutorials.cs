using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

public partial class FrontedWindowsView : ITutorialOwner<FrontedWindowsView>
{
    /// <summary>前台窗口视图教程 Key。</summary>
    public const string TutorialPageKey = "Page.FrontManage.Windows";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>前台窗口视图教程包引用。</summary>
    public static class Tours
    {
        /// <summary>前台管理 BP 窗口启动包引用。</summary>
        public static readonly TutorialPackageRef BpWindowLaunchBasic = new(TutorialPackageIds.FrontManageBpWindowLaunchBasic);

        /// <summary>窗口管理基础包引用。</summary>
        public static readonly TutorialPackageRef WindowsBasic = new(TutorialPackageIds.FrontManageWindowsBasic);

        /// <summary>打开设计器 v3 包引用。</summary>
        public static readonly TutorialPackageRef OpenDesigner = new(TutorialPackageIds.FrontManageOpenDesigner);
    }

    /// <summary>前台窗口视图教程目标名称。</summary>
    public static class TutorialTargets
    {
        /// <summary>来自项模板的首个可管理窗口卡片目标名称。</summary>
        public const string FirstManageableWindowCard = "FirstManageableWindowCard";
    }

    /// <summary>
    /// 注册前台窗口视图所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForRegion<FrontedWindowsView>()
            .Package(Tours.WindowsBasic)
                .StepKey("Step.FrontManageWindowsBasic.0.Title")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .TextKey("Step.FrontManageWindowsBasic.0.Description")
                    .TargetName(nameof(ManageableWindowGroupsPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageWindowsBasic.1.Title")
                    .TextKey("Step.FrontManageWindowsBasic.1.Description")
                    .TargetName(nameof(OpenFrontedDesignerButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageWindowsBasic.2.Title")
                    .TextKey("Step.FrontManageWindowsBasic.2.Description")
                    .TargetName(nameof(OpenAllFrontedWindowsButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageWindowsBasic.3.Title")
                    .TextKey("Step.FrontManageWindowsBasic.3.Description")
                    .TargetName(nameof(CloseAllFrontedWindowsButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageWindowsBasic.4.Title")
                    .TextKey("Step.FrontManageWindowsBasic.4.Description")
                    .TargetName(nameof(StopAllLoopAnimationsButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageWindowsBasic.5.Title")
                    .TextKey("Step.FrontManageWindowsBasic.5.Description")
                    .TargetName(nameof(ManageableWindowGroupsPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.FrontManageWindowsBasic.6.Title")
                    .TextKey("Step.FrontManageWindowsBasic.6.Description")
                    .TargetName(TutorialTargets.FirstManageableWindowCard)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.OpenDesigner)
                .StepKey("Step.FrontManageOpenDesigner.0.Title")
                    .TextKey("Step.FrontManageOpenDesigner.0.Description")
                    .TargetName(nameof(OpenFrontedDesignerButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.DesignerV3Opened)
            .Package(Tours.BpWindowLaunchBasic)
                .StepKey("Step.FrontManageBpWindowLaunchBasic.0.Title")
                    .TextKey("Step.FrontManageBpWindowLaunchBasic.0.Description")
                    .TargetTag(FrontedWindowHelper.GetFrontedWindowCanonicalId(FrontedWindowType.BpWindow))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.BpWindowOpened)
                    .PostStepAction((context, _) =>
                    {
                        ((MainWindow)context.Services.GetRequiredService<INavigationWindow>()).Activate();
                        return Task.CompletedTask;
                    })
            .Build();
    }

}
