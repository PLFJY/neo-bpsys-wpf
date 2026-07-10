using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow : ITutorialOwner<FrontedBehaviorAnimationEditorWindow>
{
    /// <summary>Designer v3 animation editor tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3AnimationEditor;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Designer v3 animation editor tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Animation editor overview package reference.</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3AnimationEditorOverview);

        /// <summary>Animation editor timeline package reference.</summary>
        public static readonly TutorialPackageRef NodeBasic = new(TutorialPackageIds.DesignerV3AnimationEditorTimelineBasic);

        /// <summary>Animation editor key frame package reference.</summary>
        public static readonly TutorialPackageRef KeyFrameBasic = new(TutorialPackageIds.DesignerV3AnimationEditorKeyFrameBasic);

        /// <summary>Animation editor preview package reference.</summary>
        public static readonly TutorialPackageRef PreviewBasic = new(TutorialPackageIds.DesignerV3AnimationEditorPreviewBasic);

        /// <summary>Animation editor help package reference.</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3AnimationEditorHelpBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the Designer v3 animation editor.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<FrontedBehaviorAnimationEditorWindow>()
            .Package(Tours.Overview)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    Lines =
                    [
                        "欢迎来到动画编辑器。",
                        "在这里你可以为前台的控件设置动画"
                    ]
                })
                .Step("动画编辑器")
                    .Text("这里编辑行为对应的动画动作。动画由时间、步骤和参数变化组成。")
                    .TargetName(nameof(AnimationTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("动画编辑器")
                    .Text("长按左键框选可以选择多个节点")
                    .TargetName(nameof(AnimationTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("动画编辑器")
                    .Text("按住 Ctrl + 鼠标滚轮可以缩放画布，按住右键移动画布")
                    .TargetName(nameof(AnimationTabs))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.NodeBasic)
                .Step("节点步骤")
                    .Text("动画会按节点步骤顺序执行。你可以先理解结构，不需要为了完成教程创建关键帧。")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Step("节点步骤")
                    .Text("动画按步骤顺序执行。不同阶段可以分别编辑进入、循环和停止动作。")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("节点步骤")
                    .Text("每个动画从“开始”节点开始，“结束”节点结束")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("动画属性节点")
                    .Text("现在试着从左侧节点目录中拖出一个“动画属性”节点到画布上并选中它。选中后教程会自动继续；如果你已经有该节点，直接点选它即可。")
                    .TargetName(TutorialTargetNames.AnimationGraphCanvas)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .Placement(ProductTourPlacement.Right)
                    .WaitFor(TutorialSignalIds.AnimationPropertyNodeSelected)
            .Package(Tours.KeyFrameBasic)
                .Step("动作参数")
                    .Text("参数用于描述位置、透明度、大小、颜色等变化。本教程不要求你创建或保存参数。")
                    .TargetName(TutorialTargetNames.AnimationPropertyPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("动作参数")
                    .Text("选中节点后，可以在这里编辑位置、透明度、大小、颜色等变化参数。")
                    .TargetName(TutorialTargetNames.AnimationPropertyPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PreviewBasic)
                .Step("预览动画")
                    .Text("预览用于确认动作顺序和参数效果。本教程不强制保存动画。")
                    .TargetName(nameof(PreviewButtonsPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Step("预览动画")
                    .Text("正式使用前建议预览动画效果，确认动作顺序和参数符合预期。确认无误后，可以按当前窗口的保存提示保存或应用；本教程不强制保存。")
                    .TargetName(TutorialTargetNames.AnimationPreviewPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("预览动画")
                    .Text("按住 Ctrl + 鼠标滚轮可以缩放预览区域，按住右键移动画布")
                    .TargetName(TutorialTargetNames.AnimationPreviewPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .Step("动画进阶说明")
                    .Text("右下角这个帮助按钮可以查看动画编辑器的详细 / 进阶说明。")
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
