using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedDesignerWindow
{
    /// <summary>Designer v3 window tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3;

    /// <summary>Designer v3 tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Designer v3 overview package id.</summary>
        public const string Overview = TutorialPackageIds.DesignerV3Overview;

        /// <summary>Designer v3 layout edit package id.</summary>
        public const string LayoutEditBasic = TutorialPackageIds.DesignerV3LayoutEditBasic;

        /// <summary>Designer v3 property panel package id.</summary>
        public const string PropertyPanelBasic = TutorialPackageIds.DesignerV3PropertyPanelBasic;

        /// <summary>Designer v3 behavior edit package id.</summary>
        public const string BehaviorEditBasic = TutorialPackageIds.DesignerV3BehaviorEditBasic;

        /// <summary>Designer v3 package import/export package id.</summary>
        public const string PackageImportExport = TutorialPackageIds.DesignerV3PackageImportExport;

        /// <summary>Designer v3 help package id.</summary>
        public const string HelpBasic = TutorialPackageIds.DesignerV3HelpBasic;
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
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.Overview,
            TutorialPackages.LayoutEditBasic,
            TutorialPackages.PropertyPanelBasic,
            TutorialPackages.PackageImportExport,
            TutorialPackages.HelpBasic
        ]);

        registrar.RegisterPackage(CreateOverviewPackage());
        registrar.RegisterPackage(CreateLayoutEditPackage());
        registrar.RegisterPackage(CreatePropertyPanelPackage());
        registrar.RegisterPackage(CreatePackageImportExportPackage());
        registrar.RegisterPackage(CreateHelpPackage());
    }

    private static TutorialPackageDefinition CreateOverviewPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.Overview,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "欢迎来到 v3 编辑器",
                    "在这里你可以详细修改前台界面，包括布局位置、控件属性、图片资源和动画行为。",
                    ProductTourInteractionMode.BlockAll),
                TutorialDefinitionHelpers.Step(
                    nameof(DesignerToolbarHost),
                    "v3 前台设计器",
                    "这是 v3 前台设计器。顶部是保存、导入、导出、缩放等工具。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(LayerPanelHostGrid),
                    "图层区域",
                    "左侧是控件和图层列表。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(PreviewWorkspace),
                    "布局预览",
                    "中间是布局预览区域。你可以在这里观察前台最终效果。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(PropertyPanelHost),
                    "属性区域",
                    "右侧是属性编辑区域。稍后在预览区域选中控件后，这里会显示可编辑属性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(BehaviorPanelHost),
                    "行为和动画区域",
                    "选中控件后，右侧会出现行为和动画区域。这些高级编辑器会在进入对应区域时单独说明。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]);

    private static TutorialPackageDefinition CreateLayoutEditPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.LayoutEditBasic,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(LayerPanelHostGrid),
                    "图层列表",
                    "图层列表显示当前布局里的控件。图层顺序会影响控件显示层级。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(LayerPanelScrollViewer),
                    "调整图层顺序",
                    "可以通过拖拽调整图层顺序。本教程不强制你拖拽图层。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(PreviewZoomHost),
                    "预览画布",
                    "中间画布显示最终前台效果。请试着点击画布上的一个控件来选中它；如果当前没有布局控件，也可以直接继续。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(InteractionLayer),
                    "交互层",
                    "交互层负责选中、拖动和缩放设计控件。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]);

    private static TutorialPackageDefinition CreatePropertyPanelPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.PropertyPanelBasic,
            TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(PropertyPanelHost),
                    "属性面板",
                    "选中控件后，右侧会显示属性。文本、颜色、图片、字体等都在这里修改。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargets.PropertySearchBox,
                    "属性筛选",
                    "这里可以筛选属性。如果没有选中控件，属性面板可能为空。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargets.FirstEditablePropertyEditor,
                    "属性编辑器",
                    "不同属性会显示不同编辑器。有些属性需要点击确认按钮才会应用。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargets.PropertyApplyButton,
                    "应用属性",
                    "如果属性非法，会显示错误状态。本教程不强制你修改或应用属性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]);

    private static TutorialPackageDefinition CreatePackageImportExportPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.PackageImportExport,
            TutorialPageKey,
            4,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(SaveLayoutButton),
                    "保存布局",
                    "修改布局后需要保存。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargets.ExportLayoutPackageButton,
                    "导出布局包",
                    "可以将布局导出为布局包。当前版本的布局包导出入口在前台管理页面的布局包区域。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    "ImportLayoutPackageButton",
                    "导入布局包",
                    "布局包也可以在前台管理页面导入和启用。如果布局包依赖插件，需要先安装对应插件。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
            ]);

    private static TutorialPackageDefinition CreateHelpPackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.HelpBasic,
            TutorialPageKey,
            5,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(DesignerHelpButton),
                    "查看详细说明",
                    "右下角这个帮助按钮可以打开 v3 编辑器的详细说明。遇到属性、行为、动画或布局包规则不清楚时，可以点击这里查看详细 / 进阶说明。",
                    ProductTourInteractionMode.AllowTargetOnly)
            ]);
}
