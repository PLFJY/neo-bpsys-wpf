using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.FrontedDesigner;

public partial class BehaviorPanelView : ITutorialOwner<BehaviorPanelView>
{
    /// <summary>设计器 v3 行为面板教程键。</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3BehaviorPanel;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>设计器 v3 行为面板教程包引用。</summary>
    public static class Tours
    {
        /// <summary>行为面板概览包引用。</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3BehaviorPanelOverview);

        /// <summary>行为面板触发器包引用。</summary>
        public static readonly TutorialPackageRef TriggerBasic = new(TutorialPackageIds.DesignerV3BehaviorPanelTriggerBasic);

        /// <summary>行为面板动作包引用。</summary>
        public static readonly TutorialPackageRef ActionBasic = new(TutorialPackageIds.DesignerV3BehaviorPanelActionBasic);

        /// <summary>行为面板帮助包引用。</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic);
    }

    /// <summary>行为项数据模板内声明的稳定目标名称。</summary>
    public static class TutorialTargets
    {
        /// <summary>行为触发器编辑器目标。</summary>
        public const string BehaviorTriggerEditor = "BehaviorTriggerEditor";

        /// <summary>行为动作编辑器目标。</summary>
        public const string BehaviorActionEditor = "BehaviorActionEditor";
    }

    /// <summary>
    /// 注册设计器 v3 行为面板拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
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
