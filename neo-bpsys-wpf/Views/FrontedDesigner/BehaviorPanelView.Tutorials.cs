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

    /// <summary>
    /// Registers tutorials owned by the Designer v3 behavior panel.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForRegion<BehaviorPanelView>()
            .AutoRun(TutorialAutoRunStrategy.DrainSequence)
            .Package(Tours.Overview)
                .Step("行为面板")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .PreStepAction(TutorialStepActions.WaitUntil(
                        "Behavior panel has selected control",
                        context => context.Owner is BehaviorPanelView { DataContext: BehaviorPanelViewModel { HasSelectedControl: true } },
                        TimeSpan.FromSeconds(2)))
                    .Text("行为由“触发条件 + 动作”组成。一次性行为、循环行为、过渡行为用途不同，可以分别处理点击后动画、持续循环和状态切换过渡。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
                .Step("新增行为")
                    .Text("这里可以新增一次性 / 循环 / 过渡行为。本教程不强制创建行为。")
                    .TargetName(nameof(AddBehaviorButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Step("行为列表")
                    .Text("每个行为都可以启用、复制、删除，并展开查看触发条件和动作配置。")
                    .TargetName(nameof(BehaviorList))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.TriggerBasic)
                .Step("触发条件")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .PreStepAction(TutorialStepActions.WaitUntil(
                        "Behavior panel has selected control",
                        context => context.Owner is BehaviorPanelView { DataContext: BehaviorPanelViewModel { HasSelectedControl: true } },
                        TimeSpan.FromSeconds(2)))
                    .Text("触发条件决定动画什么时候运行；没有创建行为时，可以先了解概念，之后展开具体行为再编辑。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
                .Step("触发条件")
                    .Text("触发条件决定动画什么时候运行，例如按状态、事件或 payload 字段过滤。")
                    .TargetName("BehaviorTriggerEditor")
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.ActionBasic)
                .Step("动作编辑")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .PreStepAction(TutorialStepActions.WaitUntil(
                        "Behavior panel has selected control",
                        context => context.Owner is BehaviorPanelView { DataContext: BehaviorPanelViewModel { HasSelectedControl: true } },
                        TimeSpan.FromSeconds(2)))
                    .Text("动作决定控件如何变化。你可以在需要时进入动画编辑器，不需要为了完成教程而创建或保存动画。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
                .Step("动作编辑")
                    .Text("点击后会进入动画编辑器，编辑具体动作、关键帧和参数。本教程不强制打开动画编辑器。")
                    .TargetName("BehaviorActionEditor")
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .Step("行为系统说明")
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(TutorialStepActions.WaitForDispatcherIdle())
                    .PreStepAction(TutorialStepActions.WaitUntil(
                        "Behavior panel has selected control",
                        context => context.Owner is BehaviorPanelView { DataContext: BehaviorPanelViewModel { HasSelectedControl: true } },
                        TimeSpan.FromSeconds(2)))
                    .PreStepAction(ScrollBehaviorHelpButtonIntoViewAction())
                    .Text("右下角这个帮助按钮可以查看行为系统的详细 / 进阶说明。")
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
