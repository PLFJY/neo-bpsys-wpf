using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.FrontedDesigner;

public partial class BehaviorPanelView : ITutorialOwner<BehaviorPanelView>
{
    /// <summary>Designer v3 behavior panel tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3BehaviorPanel;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Designer v3 behavior panel tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Behavior panel overview package reference.</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3BehaviorPanelOverview);

        /// <summary>Behavior panel trigger package reference.</summary>
        public static readonly TutorialPackageRef TriggerBasic = new(TutorialPackageIds.DesignerV3BehaviorPanelTriggerBasic);

        /// <summary>Behavior panel action package reference.</summary>
        public static readonly TutorialPackageRef ActionBasic = new(TutorialPackageIds.DesignerV3BehaviorPanelActionBasic);

        /// <summary>Behavior panel help package reference.</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic);
    }

    /// <summary>Stable target names declared inside behavior item data templates.</summary>
    public static class TutorialTargets
    {
        /// <summary>Behavior trigger editor target.</summary>
        public const string BehaviorTriggerEditor = "BehaviorTriggerEditor";

        /// <summary>Behavior action editor target.</summary>
        public const string BehaviorActionEditor = "BehaviorActionEditor";
    }

    /// <summary>
    /// Registers tutorials owned by the Designer v3 behavior panel.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForRegion<BehaviorPanelView>()
            .Package(Tours.Overview)
                .StepKey("Step.DesignerV3BehaviorPanelOverview.0.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelOverview.0.Description")
                    .TargetName(nameof(BehaviorPanel))
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(BehaviorPanel)))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3BehaviorPanelOverview.1.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelOverview.1.Description")
                    .TargetName(nameof(AddBehaviorButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3BehaviorPanelOverview.2.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelOverview.2.Description")
                    .TargetName(nameof(BehaviorList))
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(BehaviorList)))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.TriggerBasic)
                .StepKey("Step.DesignerV3BehaviorPanelTriggerBasic.0.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelTriggerBasic.0.Description")
                    .TargetName(nameof(BehaviorPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3BehaviorPanelTriggerBasic.1.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelTriggerBasic.1.Description")
                    .TargetName(nameof(BehaviorPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.ActionBasic)
                .StepKey("Step.DesignerV3BehaviorPanelActionBasic.0.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelActionBasic.0.Description")
                    .TargetName(TutorialTargets.BehaviorActionEditor)
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(TutorialTargets.BehaviorActionEditor))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3BehaviorPanelActionBasic.1.Title")
                    .TextKey("Step.DesignerV3BehaviorPanelActionBasic.1.Description")
                    .TargetName(TutorialTargets.BehaviorActionEditor)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .StepKey("Step.DesignerV3BehaviorPanelHelpBasic.0.Title")
                    .PreStepAction(ScrollBehaviorHelpButtonIntoViewAction())
                    .TextKey("Step.DesignerV3BehaviorPanelHelpBasic.0.Description")
                    .TargetName(nameof(BehaviorHelpButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Build();
    }

    private static TutorialStepAction ScrollBehaviorHelpButtonIntoViewAction() =>
        new("ScrollBehaviorHelpButtonIntoView", (context, cancellationToken) =>
            ScrollBehaviorHelpButtonIntoViewAsync(context.Services, cancellationToken));

    private static async Task ScrollBehaviorHelpButtonIntoViewAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        _ = serviceProvider;
        if (Application.Current?.Windows
                .OfType<Window>()
                .Select(window => FindBehaviorPanel(window))
                .FirstOrDefault(panel => panel is { IsVisible: true }) is not { } behaviorPanel)
        {
            return;
        }

        await behaviorPanel.Dispatcher.InvokeAsync(
            () =>
            {
                behaviorPanel.BehaviorHelpButton.BringIntoView();
                behaviorPanel.UpdateLayout();
            },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }

    private static BehaviorPanelView? FindBehaviorPanel(DependencyObject root)
    {
        if (root is BehaviorPanelView panel)
        {
            return panel;
        }

        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var nested = FindBehaviorPanel(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
