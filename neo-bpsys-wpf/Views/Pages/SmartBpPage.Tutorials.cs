using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class SmartBpPage : ITutorialOwner<SmartBpPage>
{
    /// <summary>Smart BP page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.SmartBp;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Smart BP page tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Smart BP module shell package reference.</summary>
        public static readonly TutorialPackageRef ModuleShell = new(TutorialPackageIds.SmartBpModuleShell);
    }

    /// <summary>
    /// Registers tutorials owned by the Smart BP host page.
    /// Only host-shell controls (module path, load, import) are taught here.
    /// Module-content tutorials are registered by the SmartBP module itself.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<SmartBpPage>()
            .Package(Tours.ModuleShell)
                .Step("智慧 BP")
                    .Text("智慧 BP 是独立模块，首次进入后会提供捕获、识别区域和自动识别教程。如果模块尚未加载，可以在此页面选择模块路径并加载。")
                    .TargetName(nameof(SmartBpModuleContentHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("模块路径")
                    .Text("在此输入或选择 SmartBP 模块目录路径。")
                    .TargetName(nameof(SmartBpModulePathTextBox))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("加载模块")
                    .Text("选择路径后，点击加载按钮加载本地 SmartBP 模块。")
                    .TargetName(nameof(SmartBpLoadLocalModuleButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("导入模块")
                    .Text("也可以导入 SmartBP 模块压缩包。")
                    .TargetName(nameof(SmartBpImportModuleArchiveButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }
}
