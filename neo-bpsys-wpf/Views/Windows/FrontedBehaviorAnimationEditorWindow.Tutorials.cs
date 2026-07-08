using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow
{
    /// <summary>Designer v3 animation editor tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3AnimationEditor;

    /// <summary>Designer v3 animation editor tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Animation editor overview package id.</summary>
        public const string Overview = TutorialPackageIds.DesignerV3AnimationEditorOverview;

        /// <summary>Animation editor timeline package id.</summary>
        public const string TimelineBasic = TutorialPackageIds.DesignerV3AnimationEditorTimelineBasic;

        /// <summary>Animation editor key frame package id.</summary>
        public const string KeyFrameBasic = TutorialPackageIds.DesignerV3AnimationEditorKeyFrameBasic;

        /// <summary>Animation editor preview package id.</summary>
        public const string PreviewBasic = TutorialPackageIds.DesignerV3AnimationEditorPreviewBasic;

        /// <summary>Animation editor help package id.</summary>
        public const string HelpBasic = TutorialPackageIds.DesignerV3AnimationEditorHelpBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the Designer v3 animation editor.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.Overview,
            TutorialPackages.TimelineBasic,
            TutorialPackages.KeyFrameBasic,
            TutorialPackages.PreviewBasic,
            TutorialPackages.HelpBasic
        ],
        TutorialAutoRunStrategy.DrainSequence);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Overview,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "动画编辑器",
                    "这里编辑行为对应的动画动作。动画由时间、步骤和参数变化组成。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    nameof(AnimationTabs),
                    "动画编辑器",
                    "这里编辑行为对应的动画动作。动画由时间、步骤和参数变化组成。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.TimelineBasic,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "时间和步骤",
                    "动画会按时间和步骤顺序执行。你可以先理解结构，不需要为了完成教程创建关键帧。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.AnimationGraphCanvas,
                    "时间顺序",
                    "动画按时间和步骤顺序执行。不同阶段可以分别编辑进入、循环和停止动作。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.KeyFrameBasic,
            TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "动作参数",
                    "参数用于描述位置、透明度、大小、颜色等变化。本教程不要求你创建或保存参数。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.AnimationPropertyPanel,
                    "动作参数",
                    "选中节点后，可以在这里编辑位置、透明度、大小、颜色等变化参数。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.PreviewBasic,
            TutorialPageKey,
            4,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "预览动画",
                    "预览用于确认动作顺序和参数效果。本教程不强制保存动画。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.AnimationPreviewPanel,
                    "预览动画",
                    "正式使用前建议预览动画效果，确认动作顺序和参数符合预期。确认无误后，可以按当前窗口的保存提示保存或应用；本教程不强制保存。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.HelpBasic,
            TutorialPageKey,
            5,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(AnimationEditorHelpButton),
                    "动画进阶说明",
                    "右下角这个帮助按钮可以查看动画编辑器的详细 / 进阶说明。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    beforeShowAsync: ScrollAnimationHelpButtonIntoViewAsync,
                    scrollAnchorName: nameof(AnimationEditorHelpButton))
            ]));
    }

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
