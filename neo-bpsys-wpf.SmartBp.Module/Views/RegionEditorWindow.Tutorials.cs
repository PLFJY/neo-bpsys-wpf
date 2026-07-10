using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Views.Windows;

public partial class RegionEditorWindow : ITutorialOwner<RegionEditorWindow>
{
    /// <summary>Region editor window tutorial key.</summary>
    public const string TutorialPageKey = "Window.SmartBp.RegionEditor";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Region editor window tutorial package ids.</summary>
    public static class PackageIds
    {
        /// <summary>Smart BP region editor package id.</summary>
        public const string RegionEditorBasic = "Page.SmartBp.RegionEditor.Basic";
    }

    /// <summary>Region editor window tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Smart BP region editor package reference.</summary>
        public static readonly TutorialPackageRef RegionEditorBasic = new(PackageIds.RegionEditorBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the RegionEditorWindow.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<RegionEditorWindow>()
            .Package(Tours.RegionEditorBasic)
                .Step("区域预览")
                    .Text("如果识别不准，优先检查识别区域是否对齐。可以通过预览画面调整区域。")
                    .TargetName(nameof(SmartBpRegionPreviewPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("区域列表")
                    .Text("这里列出可配置的识别区域。本教程不强制拖拽区域。")
                    .TargetName(nameof(SmartBpRegionListPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("保存区域")
                    .Text("保存区域后再开始识别。本教程不强制保存，也不等待保存完成。")
                    .TargetName(nameof(SmartBpSaveRegionButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }
}
