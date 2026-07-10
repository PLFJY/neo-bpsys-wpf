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

        /// <summary>Designer v3 behavior edit package reference.</summary>
        public static readonly TutorialPackageRef BehaviorEditBasic = new(TutorialPackageIds.DesignerV3BehaviorEditBasic);

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
                .Step("欢迎来到 v3 设计器")
                    .Text("在这里你可以详细修改前台界面，包括布局位置、控件属性、图片资源和动画行为。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
                .Step("工具栏")
                    .Text("这是 v3 前台设计器的工具栏。顶部是保存、导入、导出、缩放等工具。")
                    .TargetName(nameof(DesignerToolbarHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("图层区域")
                    .Text("左侧是控件和图层列表。")
                    .TargetName(nameof(LayerPanelHostGrid))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("布局预览")
                    .Text("中间是布局预览区域。你可以在这里观察前台最终效果。")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("属性区域")
                    .Text("右侧是属性编辑区域。稍后在预览区域选中控件后，这里会显示可编辑属性。")
                    .TargetName(nameof(PropertyPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
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
                    .Text("中间画布显示最终前台效果。请试着点击画布上的一个控件来选中它；如果当前没有布局控件，也可以直接继续。")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("保存布局")
                    .Text("调整布局后，可以在顶部工具区保存当前布局。")
                    .TargetName(nameof(SaveLayoutButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PropertyPanelBasic)
                .Step("属性面板")
                    .Text("选中控件后，右侧会显示属性。文本、颜色、图片、字体等都在这里修改。")
                    .TargetName(nameof(PropertyPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("属性筛选")
                    .Text("这里可以筛选属性。如果没有选中控件，属性面板可能为空。")
                    .TargetName(TutorialTargets.PropertySearchBox)
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
                .Step("行为和动画入口")
                    .Text("选中控件后，属性面板下方会出现行为和动画区域。行为用于定义触发条件和动作；动画用于编辑具体变化效果。后续进入行为面板或动画编辑器时，会有单独详细说明。")
                    .PreStepAction(ScrollDesignerPropertyPanelToBehaviorAreaAction())
                    .TargetName(nameof(BehaviorPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("行为和动画入口")
                    .Text("如果当前没有选中控件，行为和动画区域会暂时隐藏。选中控件后，可以在属性面板下方找到行为和动画入口；这里不需要你创建行为或打开动画编辑器。")
                    .PreStepAction(ScrollDesignerPropertyPanelToBehaviorAreaAction())
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
            .Package(Tours.BehaviorEditBasic)
                .Step("行为编辑")
                    .Text("展开这里可以为当前控件配置触发条件、动作和动画。行为面板与动画编辑器会在各自界面继续介绍。")
                    .PreStepAction(ScrollDesignerPropertyPanelToBehaviorAreaAction())
                    .TargetName(nameof(BehaviorExpander))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PackageImportExport)
                .Step("保存、导入和导出")
                    .Text("这里集中说明布局保存、布局包导入导出和兼容性注意事项。部分入口可能位于前台管理页面，本教程不会要求你实际导入或导出。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
                .Step("保存布局")
                    .Text("修改布局后需要保存。")
                    .TargetName(nameof(SaveLayoutButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("导出布局包")
                    .Text("可以将布局导出为布局包。当前版本的布局包导出入口在前台管理页面的布局包区域。")
                    .TargetName(TutorialTargets.ExportLayoutPackageButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("导入布局包")
                    .Text("布局包也可以在前台管理页面导入和启用。如果布局包依赖插件，需要先安装对应插件。")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.BlockAll)
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
