using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

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
        ]);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Overview,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(AnimationTabs),
                    "动画编辑器",
                    "这里编辑行为对应的动画动作。动画由目标、时间和属性变化组成。",
                    ProductTourInteractionMode.AllowTargetOnly)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.TimelineBasic,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.AnimationGraphCanvas,
                    "时间顺序",
                    "动画按节点和连接的顺序执行。不同阶段可以分别编辑进入、循环和停止动作。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.KeyFrameBasic,
            TutorialPageKey,
            3,
            [
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
                    TutorialTargetNames.AnimationPreviewPanel,
                    "预览动画",
                    "正式使用前建议预览动画效果，确认动作顺序和参数符合预期。",
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
                    ProductTourInteractionMode.AllowTargetOnly)
            ]));
    }
}
