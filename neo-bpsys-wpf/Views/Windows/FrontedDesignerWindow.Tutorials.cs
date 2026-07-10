using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedDesignerWindow : ITutorialOwner<FrontedDesignerWindow>
{
    /// <summary>Designer v3 window tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Designer v3 tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Designer v3 overview package reference.</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3Overview);

        /// <summary>Designer v3 layout edit package reference.</summary>
        public static readonly TutorialPackageRef LayoutEditBasic = new(TutorialPackageIds.DesignerV3LayoutEditBasic);

        /// <summary>Designer v3 property panel package reference.</summary>
        public static readonly TutorialPackageRef PropertyPanelBasic = new(TutorialPackageIds.DesignerV3PropertyPanelBasic);

        /// <summary>Designer v3 package import/export package reference.</summary>
        public static readonly TutorialPackageRef PackageImportExport = new(TutorialPackageIds.DesignerV3PackageImportExport);

        /// <summary>Designer v3 help package reference.</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3HelpBasic);
    }

    /// <summary>Designer v3 tutorial target names not owned by direct window fields.</summary>
    public static class TutorialTargets
    {
        /// <summary>Property search box target name.</summary>
        public const string PropertySearchBox = "PropertySearchBox";

        /// <summary>First editable property editor target name.</summary>
        public const string FirstEditablePropertyEditor = "FirstEditablePropertyEditor";

        /// <summary>Property apply button target name.</summary>
        public const string PropertyApplyButton = "PropertyApplyButton";

        /// <summary>Behavior add button target name.</summary>
        public const string AddBehaviorButton = "AddBehaviorButton";

        /// <summary>Behavior trigger editor target name.</summary>
        public const string BehaviorTriggerEditor = "BehaviorTriggerEditor";

        /// <summary>Behavior action editor target name.</summary>
        public const string BehaviorActionEditor = "BehaviorActionEditor";

        /// <summary>Layout package export button target name.</summary>
        public const string ExportLayoutPackageButton = "ExportLayoutPackageButton";
    }

    /// <summary>
    /// Registers tutorials owned by the Designer v3 window.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<FrontedDesignerWindow>()
            .Package(Tours.Overview)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    Lines =
                    [
                        "欢迎来到 v3 设计器。",
                        "在这里你可以详细修改前台界面，包括布局位置、控件属性、图片资源和动画行为。"
                    ]
                })
            .Package(Tours.LayoutEditBasic)
                .Step("图层列表")
                    .Text("图层列表显示当前布局里的控件。图层顺序会影响控件显示层级。")
                    .TargetName(nameof(LayerPanelHostGrid))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("调整图层顺序")
                    .Text("可以通过拖拽调整图层顺序。本教程不强制你拖拽图层。")
                    .TargetName(nameof(LayerPanelScrollViewer))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("预览画布")
                    .Text("中间画布显示最终前台效果。请试着点击画布上的一个控件来选中它并移动位置；也可以直接继续。")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .Step("预览画布")
                    .Text("按住 Ctrl / 长按左键框选可以选择多个控件")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .Step("预览画布")
                    .Text("按住 Shift 可以临时开启就近吸附和参考线")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .Step("预览画布")
                    .Text("按住 Ctrl + 鼠标滚轮可以缩放画布，按住右键移动画布")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .Step("保存布局")
                    .Text("调整布局后，可以在顶部工具区保存当前布局。或直接按 Ctrl+S 保存")
                    .TargetName(nameof(SaveLayoutButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PropertyPanelBasic)
                .OnDemand()
                .Step("属性面板")
                    .Text("选中控件后，右侧会显示属性。文本、颜色、图片、字体等都在这里修改。")
                    .TargetName(nameof(PropertyPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("属性编辑器")
                    .Text("不同属性会显示不同编辑器。有些属性需要点击确认按钮才会应用。")
                    .TargetName(TutorialTargets.FirstEditablePropertyEditor)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("应用属性")
                    .Text("如果属性非法，会显示错误状态。本教程不强制你修改或应用属性。")
                    .TargetName(TutorialTargets.PropertyApplyButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .Step("查看详细说明")
                    .Text("右下角这个帮助按钮可以打开 v3 编辑器的详细说明。遇到属性、行为、动画或布局包规则不清楚时，可以点击这里查看详细 / 进阶说明。")
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
