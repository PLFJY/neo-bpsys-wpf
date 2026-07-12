using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedDesignerWindow : ITutorialOwner<FrontedDesignerWindow>
{
    /// <summary>设计器 v3 窗口教程 Key。</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>设计器 v3 教程包引用。</summary>
    public static class Tours
    {
        /// <summary>设计器 v3 概览包引用。</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3Overview);

        /// <summary>设计器 v3 布局编辑包引用。</summary>
        public static readonly TutorialPackageRef LayoutEditBasic = new(TutorialPackageIds.DesignerV3LayoutEditBasic);

        /// <summary>设计器 v3 属性面板包引用。</summary>
        public static readonly TutorialPackageRef PropertyPanelBasic = new(TutorialPackageIds.DesignerV3PropertyPanelBasic);

        /// <summary>设计器 v3 包导入/导出包引用。</summary>
        public static readonly TutorialPackageRef PackageImportExport = new(TutorialPackageIds.DesignerV3PackageImportExport);

        /// <summary>设计器 v3 帮助包引用。</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3HelpBasic);
    }

    /// <summary>设计器 v3 教程目标名称，不属于窗口直接字段。</summary>
    public static class TutorialTargets
    {
        /// <summary>属性搜索框目标名称。</summary>
        public const string PropertySearchBox = "PropertySearchBox";

        /// <summary>首个可编辑属性编辑器目标名称。</summary>
        public const string FirstEditablePropertyEditor = "FirstEditablePropertyEditor";

        /// <summary>属性应用按钮目标名称。</summary>
        public const string PropertyApplyButton = "PropertyApplyButton";

        /// <summary>行为添加按钮目标名称。</summary>
        public const string AddBehaviorButton = "AddBehaviorButton";

        /// <summary>行为触发器编辑器目标名称。</summary>
        public const string BehaviorTriggerEditor = "BehaviorTriggerEditor";

        /// <summary>行为动作编辑器目标名称。</summary>
        public const string BehaviorActionEditor = "BehaviorActionEditor";

        /// <summary>布局包导出按钮目标名称。</summary>
        public const string ExportLayoutPackageButton = "ExportLayoutPackageButton";
    }

    /// <summary>
    /// 注册设计器 v3 窗口所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<FrontedDesignerWindow>()
            .Package(Tours.Overview)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    LinesKey = "Dialogue.DesignerV3Overview.Lines"
                })
            .Package(Tours.LayoutEditBasic)
                .StepKey("Step.DesignerV3LayoutEditBasic.0.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.0.Description")
                    .TargetName(nameof(LayerPanelHostGrid))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.1.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.1.Description")
                    .TargetName(nameof(LayerPanelScrollViewer))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.2.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.2.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.3.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.3.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.4.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.4.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.5.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.5.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.6.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.6.Description")
                    .TargetName(nameof(SaveLayoutButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PropertyPanelBasic)
                .OnDemand()
                .StepKey("Step.DesignerV3PropertyPanelBasic.0.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.0.Description")
                    .TargetName(nameof(PropertyPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3PropertyPanelBasic.1.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.1.Description")
                    .TargetName(TutorialTargets.FirstEditablePropertyEditor)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3PropertyPanelBasic.2.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.2.Description")
                    .TargetName(TutorialTargets.PropertyApplyButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3PropertyPanelBasic.3.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.3.Description")
                    .PreStepAction(ScrollDesignerPropertyPanelToBehaviorAreaAction())
                    .TargetName(nameof(BehaviorPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PackageImportExport)
                .StepKey("Step.DesignerV3PackageImportExport.0.Title")
                    .TextKey("Step.DesignerV3PackageImportExport.0.Description")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3PackageImportExport.1.Title")
                    .TextKey("Step.DesignerV3PackageImportExport.1.Description")
                    .TargetName(TutorialTargets.ExportLayoutPackageButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .StepKey("Step.DesignerV3HelpBasic.0.Title")
                    .TextKey("Step.DesignerV3HelpBasic.0.Description")
                    .PreStepAction(ScrollDesignerHelpButtonIntoViewAction())
                    .TargetName(nameof(DesignerHelpButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Build();
    }

    private static TutorialStepAction ScrollDesignerPropertyPanelToBehaviorAreaAction() =>
        new("ScrollDesignerPropertyPanelToBehaviorArea", (context, cancellationToken) =>
            ScrollDesignerPropertyPanelToBehaviorAreaAsync(context.Services, cancellationToken));

    private static TutorialStepAction ScrollDesignerHelpButtonIntoViewAction() =>
        new("ScrollDesignerHelpButtonIntoView", (context, cancellationToken) =>
            ScrollDesignerHelpButtonIntoViewAsync(context.Services, cancellationToken));

    private static async Task ScrollDesignerPropertyPanelToBehaviorAreaAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        _ = serviceProvider;
        if (FindActiveDesignerWindow() is not { } window)
        {
            return;
        }

        await window.Dispatcher.InvokeAsync(
            () =>
            {
                if (window.BehaviorPanelHost.IsVisible)
                {
                    window.BehaviorPanelHost.BringIntoView();
                }
                else if (window.AnimationPartsPanelHost.IsVisible)
                {
                    window.AnimationPartsPanelHost.BringIntoView();
                }
                else
                {
                    window.PropertyPanelScrollViewer.ScrollToBottom();
                }

                window.UpdateLayout();
            },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }

    private static async Task ScrollDesignerHelpButtonIntoViewAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        _ = serviceProvider;
        if (FindActiveDesignerWindow() is not { } window)
        {
            return;
        }

        await window.Dispatcher.InvokeAsync(
            () =>
            {
                window.DesignerHelpButton.BringIntoView();
                window.UpdateLayout();
            },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }

    private static FrontedDesignerWindow? FindActiveDesignerWindow() =>
        Application.Current?.Windows
            .OfType<FrontedDesignerWindow>()
            .FirstOrDefault(window => window.IsActive)
        ?? Application.Current?.Windows
            .OfType<FrontedDesignerWindow>()
            .FirstOrDefault(window => window.IsVisible);
}
