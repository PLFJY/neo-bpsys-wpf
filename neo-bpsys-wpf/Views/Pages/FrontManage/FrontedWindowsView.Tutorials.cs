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
    /// <summary>Fronted windows view tutorial key.</summary>
    public const string TutorialPageKey = "Page.FrontManage.Windows";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Fronted windows view tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Front management BP window launch package reference.</summary>
        public static readonly TutorialPackageRef BpWindowLaunchBasic = new(TutorialPackageIds.FrontManageBpWindowLaunchBasic);

        /// <summary>Window management basic package reference.</summary>
        public static readonly TutorialPackageRef WindowsBasic = new(TutorialPackageIds.FrontManageWindowsBasic);

        /// <summary>Open Designer v3 package reference.</summary>
        public static readonly TutorialPackageRef OpenDesigner = new(TutorialPackageIds.FrontManageOpenDesigner);
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
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForRegion<FrontedWindowsView>()
            .AutoRun(TutorialAutoRunStrategy.ContinueWhileActive)
            .Package(Tours.WindowsBasic)
                .Step("前台窗口")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .Text("这里管理所有前台窗口。导播排查和控制窗口时，可以在这里查看窗口列表。")
                    .TargetName(nameof(ManageableWindowGroupsPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("打开设计器")
                    .Text("点击这里可以打开 v3 前台设计器，用来编辑前台布局、控件属性和动画行为。")
                    .TargetName(nameof(OpenFrontedDesignerButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("打开全部窗口")
                    .Text("“打开全部”会一次性打开所有前台窗口，适合导播开播前检查 OBS 捕获来源。它可能同时弹出多个窗口，本教程不会要求你必须点击。")
                    .TargetName(nameof(OpenAllFrontedWindowsButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("关闭全部窗口")
                    .Text("“关闭全部”会一次性关闭所有前台窗口，适合导播收尾或排查窗口状态。本教程不会等待关闭操作完成。")
                    .TargetName(nameof(CloseAllFrontedWindowsButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("停止循环动画")
                    .Text("如果前台循环动画异常，可以使用“停止所有循环动画”。这是导播排查动画状态时的控制功能。")
                    .TargetName(nameof(StopAllLoopAnimationsButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("窗口分组")
                    .Text("这里按分组列出可管理的前台窗口。每个窗口都可以单独打开或关闭。")
                    .TargetName(nameof(ManageableWindowGroupsPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("单个窗口")
                    .Text("单个窗口卡片可以独立打开或关闭对应前台窗口。")
                    .TargetName(TutorialTargets.FirstManageableWindowCard)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.OpenDesigner)
                .Step("打开 v3 编辑器")
                    .Text("v3 编辑器用于编辑前台布局、控件属性和动画行为。点击这里可以打开前台设计器。打开后会进入独立的 v3 编辑器教程。")
                    .TargetName(nameof(OpenFrontedDesignerButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction(ScheduleDesignerTutorialAction())
                    .WaitFor(TutorialSignalIds.DesignerV3Opened)
            .Package(Tours.BpWindowLaunchBasic)
                .Step("启动 BP 前台窗口")
                    .Text("导播时，观众看到的是前台窗口。我们先只启动 BP 前台页面。")
                    .TargetTag(FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.BpWindowOpened)
                    .PostStepAction((_, _) =>
                    {
                        ((MainWindow)IAppHost.Host!.Services.GetRequiredService<INavigationWindow>()).Activate();
                        return Task.CompletedTask;
                    })
            .Build();
    }

    private static TutorialStepAction ScheduleDesignerTutorialAction() =>
        new("ScheduleDesignerTutorial", (context, cancellationToken) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            Application.Current?.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() =>
                {
                    var window = Application.Current.Windows
                        .OfType<FrontedDesignerWindow>()
                        .FirstOrDefault(candidate => candidate.IsVisible);
                    if (window is not null)
                    {
                        var runner = context.Services.GetService<ITutorialRunner>();
                        _ = runner?.RunUntilBlockedAsync(window, TutorialPageKeys.DesignerV3);
                    }
                }));
            return Task.CompletedTask;
        });
}
