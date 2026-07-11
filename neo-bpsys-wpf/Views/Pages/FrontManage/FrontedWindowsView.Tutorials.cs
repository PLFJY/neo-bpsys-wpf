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

}
