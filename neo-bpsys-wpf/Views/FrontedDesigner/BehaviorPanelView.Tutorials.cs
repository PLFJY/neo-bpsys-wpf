using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.FrontedDesigner;

public partial class BehaviorPanelView
{
    /// <summary>Designer v3 behavior panel tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3BehaviorPanel;

    /// <summary>Designer v3 behavior panel tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Behavior panel overview package id.</summary>
        public const string Overview = TutorialPackageIds.DesignerV3BehaviorPanelOverview;

        /// <summary>Behavior panel trigger package id.</summary>
        public const string TriggerBasic = TutorialPackageIds.DesignerV3BehaviorPanelTriggerBasic;

        /// <summary>Behavior panel action package id.</summary>
        public const string ActionBasic = TutorialPackageIds.DesignerV3BehaviorPanelActionBasic;

        /// <summary>Behavior panel help package id.</summary>
        public const string HelpBasic = TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the Designer v3 behavior panel.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.Overview,
            TutorialPackages.TriggerBasic,
            TutorialPackages.ActionBasic,
            TutorialPackages.HelpBasic
        ]);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Overview,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "行为面板",
                    "行为由“触发条件 + 动作”组成。一次性行为、循环行为、过渡行为用途不同，可以分别处理点击后动画、持续循环和状态切换过渡。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    nameof(AddBehaviorButton),
                    "新增行为",
                    "这里可以新增一次性 / 循环 / 过渡行为。本教程不强制创建行为。",
                    ProductTourInteractionMode.AllowTargetOnly),
                TutorialDefinitionHelpers.Step(
                    nameof(BehaviorList),
                    "行为列表",
                    "每个行为都可以启用、复制、删除，并展开查看触发条件和动作配置。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.TriggerBasic,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "触发条件",
                    "触发条件决定动画什么时候运行；没有创建行为时，可以先了解概念，之后展开具体行为再编辑。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    "BehaviorTriggerEditor",
                    "触发条件",
                    "触发条件决定动画什么时候运行，例如按状态、事件或 payload 字段过滤。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.ActionBasic,
            TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "动作编辑",
                    "动作决定控件如何变化。你可以在需要时进入动画编辑器，不需要为了完成教程而创建或保存动画。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    "BehaviorActionEditor",
                    "动作编辑",
                    "点击后会进入动画编辑器，编辑具体动作、关键帧和参数。本教程不强制打开动画编辑器。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.HelpBasic,
            TutorialPageKey,
            4,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(BehaviorHelpButton),
                    "行为系统说明",
                    "右下角这个帮助按钮可以查看行为系统的详细 / 进阶说明。",
                    ProductTourInteractionMode.AllowTargetOnly)
            ]));
    }
}
