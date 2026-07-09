using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

public partial class FrontedLayoutPackagesView
{
    /// <summary>Fronted layout packages view tutorial key.</summary>
    public const string TutorialPageKey = "Page.FrontManage.LayoutPackages";

    /// <summary>Fronted layout package view tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Layout packages basic package id.</summary>
        public const string LayoutPackagesBasic = TutorialPackageIds.FrontManageLayoutPackagesBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the layout package view.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.LayoutPackagesBasic
        ],
        TutorialAutoRunStrategy.ContinueWhileActive);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.LayoutPackagesBasic,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(LayoutPackageList),
                    "布局包",
                    "布局包是前台界面的打包格式。可以导入别人制作的布局包，也可以切换当前启用的布局包。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(LayoutPackageList),
                    "布局包列表",
                    "这里显示已安装的布局包。旧版 bpui 包会尝试转换，部分布局包可能依赖插件。内置布局无法被直接修改。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(ImportLayoutPackageButton),
                    "导入布局包",
                    "点击导入会打开文件选择器。本教程不要求你选择文件；导入和切换布局包前，建议确认来源和兼容性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(ActivateLayoutPackageButton),
                    "启用布局包",
                    "点击启用可以切换当前前台界面方案。切换前请确认布局包来源、插件依赖和兼容性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    nameof(ActiveLayoutPackagePanel),
                    "当前布局包",
                    "这里显示当前启用的布局包和管理状态。如果你编辑内置布局，系统会自动切换到一个新的用户自定义布局，避免覆盖内置方案。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));
    }
}
