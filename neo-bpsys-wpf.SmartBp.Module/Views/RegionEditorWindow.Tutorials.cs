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
