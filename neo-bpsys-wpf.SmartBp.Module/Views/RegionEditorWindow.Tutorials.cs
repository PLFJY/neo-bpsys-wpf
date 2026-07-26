using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Views.Windows;

public partial class RegionEditorWindow : ITutorialOwner<RegionEditorWindow>
{
    /// <summary>区域编辑器窗口教程键。</summary>
    public const string TutorialPageKey = "Window.SmartBp.RegionEditor";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>区域编辑器窗口教程包 id。</summary>
    public static class PackageIds
    {
        /// <summary>SmartBP 区域编辑器包 id。</summary>
        public const string RegionEditorBasic = "Page.SmartBp.RegionEditor.Basic";
    }

    /// <summary>区域编辑器窗口教程包引用。</summary>
    public static class Tours
    {
        /// <summary>SmartBP 区域编辑器包引用。</summary>
        public static readonly TutorialPackageRef RegionEditorBasic = new(PackageIds.RegionEditorBasic);
    }

    /// <summary>
    /// 注册由 RegionEditorWindow 拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<RegionEditorWindow>()
            .Package(Tours.RegionEditorBasic)
                .StepKey("Step.SmartBpRegionEditorBasic.0.Title")
                    .TextKey("Step.SmartBpRegionEditorBasic.0.Description")
                    .TargetName(nameof(SmartBpRegionPreviewPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.SmartBpRegionEditorBasic.1.Title")
                    .TextKey("Step.SmartBpRegionEditorBasic.1.Description")
                    .TargetName(nameof(SmartBpRegionListPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.SmartBpRegionEditorBasic.2.Title")
                    .TextKey("Step.SmartBpRegionEditorBasic.2.Description")
                    .TargetName(nameof(SmartBpSaveRegionButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }
}
