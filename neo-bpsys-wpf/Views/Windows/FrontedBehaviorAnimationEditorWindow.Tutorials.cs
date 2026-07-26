using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow : ITutorialOwner<FrontedBehaviorAnimationEditorWindow>
{
    /// <summary>设计器 v3 动画编辑器教程键。</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3AnimationEditor;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>设计器 v3 动画编辑器教程包引用。</summary>
    public static class Tours
    {
        /// <summary>动画编辑器概览包引用。</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3AnimationEditorOverview);

        /// <summary>动画编辑器时间轴包引用。</summary>
        public static readonly TutorialPackageRef NodeBasic = new(TutorialPackageIds.DesignerV3AnimationEditorTimelineBasic);

        /// <summary>动画编辑器关键帧包引用。</summary>
        public static readonly TutorialPackageRef KeyFrameBasic = new(TutorialPackageIds.DesignerV3AnimationEditorKeyFrameBasic);

        /// <summary>动画编辑器预览包引用。</summary>
        public static readonly TutorialPackageRef PreviewBasic = new(TutorialPackageIds.DesignerV3AnimationEditorPreviewBasic);

        /// <summary>动画编辑器帮助包引用。</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3AnimationEditorHelpBasic);
    }

    /// <summary>
    /// 注册设计器 v3 动画编辑器拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<FrontedBehaviorAnimationEditorWindow>()
            .Package(Tours.Overview)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    LinesKey = "Dialogue.DesignerV3AnimationEditorOverview.Lines"
                })
                .StepKey("Step.DesignerV3AnimationEditorOverview.0.Title")
                    .TextKey("Step.DesignerV3AnimationEditorOverview.0.Description")
                    .TargetName(nameof(AnimationTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3AnimationEditorOverview.1.Title")
                    .TextKey("Step.DesignerV3AnimationEditorOverview.1.Description")
                    .TargetName(nameof(AnimationTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3AnimationEditorOverview.2.Title")
                    .TextKey("Step.DesignerV3AnimationEditorOverview.2.Description")
                    .TargetName(nameof(AnimationTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.NodeBasic)
                .StepKey("Step.DesignerV3AnimationEditorTimelineBasic.0.Title")
                    .TextKey("Step.DesignerV3AnimationEditorTimelineBasic.0.Description")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3AnimationEditorTimelineBasic.1.Title")
                    .TextKey("Step.DesignerV3AnimationEditorTimelineBasic.1.Description")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3AnimationEditorTimelineBasic.2.Title")
                    .TextKey("Step.DesignerV3AnimationEditorTimelineBasic.2.Description")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3AnimationEditorTimelineBasic.3.Title")
                    .TextKey("Step.DesignerV3AnimationEditorTimelineBasic.3.Description")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .Placement(ProductTourPlacement.Right)
                    .WaitFor(TutorialSignalIds.AnimationPropertyNodeSelected)
            .Package(Tours.KeyFrameBasic)
                .StepKey("Step.DesignerV3AnimationEditorKeyFrameBasic.0.Title")
                    .TextKey("Step.DesignerV3AnimationEditorKeyFrameBasic.0.Description")
                    .TargetName(TutorialTargetNames.AnimationPropertyPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3AnimationEditorKeyFrameBasic.1.Title")
                    .TextKey("Step.DesignerV3AnimationEditorKeyFrameBasic.1.Description")
                    .TargetName(TutorialTargetNames.AnimationPropertyPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PreviewBasic)
                .StepKey("Step.DesignerV3AnimationEditorPreviewBasic.0.Title")
                    .TextKey("Step.DesignerV3AnimationEditorPreviewBasic.0.Description")
                    .TargetName(nameof(PreviewButtonsPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3AnimationEditorPreviewBasic.1.Title")
                    .TextKey("Step.DesignerV3AnimationEditorPreviewBasic.1.Description")
                    .TargetName(TutorialTargetNames.AnimationPreviewPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3AnimationEditorPreviewBasic.2.Title")
                    .TextKey("Step.DesignerV3AnimationEditorPreviewBasic.2.Description")
                    .TargetName(TutorialTargetNames.AnimationPreviewPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .StepKey("Step.DesignerV3AnimationEditorHelpBasic.0.Title")
                    .TextKey("Step.DesignerV3AnimationEditorHelpBasic.0.Description")
                    .PreStepAction(ScrollAnimationHelpButtonIntoViewAction())
                    .TargetName(nameof(AnimationEditorHelpButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Build();
    }

    private static TutorialStepAction ScrollAnimationHelpButtonIntoViewAction() =>
        new("ScrollAnimationHelpButtonIntoView", (context, cancellationToken) =>
            ScrollAnimationHelpButtonIntoViewAsync(context.Services, cancellationToken));

    private static async Task ScrollAnimationHelpButtonIntoViewAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        _ = serviceProvider;
        var window = Application.Current?.Windows
            .OfType<FrontedBehaviorAnimationEditorWindow>()
            .FirstOrDefault(item => item.IsActive)
            ?? Application.Current?.Windows
                .OfType<FrontedBehaviorAnimationEditorWindow>()
                .FirstOrDefault(item => item.IsVisible);
        if (window == null)
        {
            return;
        }

        await window.Dispatcher.InvokeAsync(
            () =>
            {
                window.AnimationEditorHelpButton.BringIntoView();
                window.UpdateLayout();
            },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }
}
