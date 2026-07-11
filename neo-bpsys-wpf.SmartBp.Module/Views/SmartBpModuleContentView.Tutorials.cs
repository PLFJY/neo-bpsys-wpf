using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Views.Pages;

public partial class SmartBpModuleContentView : ITutorialOwner<SmartBpModuleContentView>
{
    /// <summary>SmartBP module content tutorial key.</summary>
    public const string TutorialPageKey = "Page.SmartBp.ModuleContent";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>SmartBP module content tutorial package ids.</summary>
    public static class PackageIds
    {
        /// <summary>Smart BP module content overview package id.</summary>
        public const string ModuleContentOverview = "Page.SmartBp.ModuleContent.Overview";

        /// <summary>Smart BP OCR model download and management package id.</summary>
        public const string OcrModelDownloadBasic = "Page.SmartBp.OcrModelDownload.Basic";

        /// <summary>Smart BP capture package id.</summary>
        public const string CaptureBasic = "Page.SmartBp.Capture.Basic";

        /// <summary>Smart BP region editor entry package id.</summary>
        public const string RegionEditorEntryBasic = "Page.SmartBp.RegionEditorEntry.Basic";

        /// <summary>Smart BP full BP flow package id.</summary>
        public const string FullBpFlowBasic = "Page.SmartBp.FullBpFlow.Basic";

        /// <summary>Smart BP post-game auto-fill package id.</summary>
        public const string PostGameAutoFill = "Page.SmartBp.PostGameAutoFill";
    }

    /// <summary>SmartBP module content tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Smart BP module content overview package reference.</summary>
        public static readonly TutorialPackageRef ModuleContentOverview = new(PackageIds.ModuleContentOverview);

        /// <summary>Smart BP OCR model download and management package reference.</summary>
        public static readonly TutorialPackageRef OcrModelDownloadBasic = new(PackageIds.OcrModelDownloadBasic);

        /// <summary>Smart BP capture basic package reference.</summary>
        public static readonly TutorialPackageRef CaptureBasic = new(PackageIds.CaptureBasic);

        /// <summary>Smart BP region editor entry package reference.</summary>
        public static readonly TutorialPackageRef RegionEditorEntryBasic = new(PackageIds.RegionEditorEntryBasic);

        /// <summary>Smart BP full BP flow package reference.</summary>
        public static readonly TutorialPackageRef FullBpFlowBasic = new(PackageIds.FullBpFlowBasic);

        /// <summary>Smart BP post-game auto-fill package reference.</summary>
        public static readonly TutorialPackageRef PostGameAutoFill = new(PackageIds.PostGameAutoFill);
    }

    /// <summary>
    /// Registers tutorials owned by the SmartBP module content view.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForRegion<SmartBpModuleContentView>()
            .Package(Tours.OcrModelDownloadBasic)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    LinesKey = "Dialogue.SmartBpOcrModelDownloadBasic.Lines"
                })
                .StepKey("Step.SmartBpOcrModelDownloadBasic.0.Title")
                    .TextKey("Step.SmartBpOcrModelDownloadBasic.0.Description")
                    .TargetName(nameof(SmartBpOcrModelManagementCard))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.CaptureBasic)
                .StepKey("Step.SmartBpCaptureBasic.0.Title")
                    .TextKey("Step.SmartBpCaptureBasic.0.Description")
                    .TargetName(nameof(SmartBpWindowSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.SmartBpCaptureBasic.1.Title")
                    .TextKey("Step.SmartBpCaptureBasic.1.Description")
                    .TargetName(nameof(SmartBpStartCaptureButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.SmartBpCaptureBasic.2.Title")
                    .TextKey("Step.SmartBpCaptureBasic.2.Description")
                    .TargetName(nameof(SmartBpPreviewButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.SmartBpCaptureBasic.3.Title")
                    .TextKey("Step.SmartBpCaptureBasic.3.Description")
                    .TargetName(nameof(SmartBpStopCaptureButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.RegionEditorEntryBasic)
                .StepKey("Step.SmartBpRegionEditorEntryBasic.0.Title")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SmartBpPostGamePreviewPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TextKey("Step.SmartBpRegionEditorEntryBasic.0.Description")
                    .TargetName(nameof(SmartBpPostGamePreviewPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.FullBpFlowBasic)
                .StepKey("Step.SmartBpFullBpFlowBasic.0.Title")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SmartBpStartFullBpFlowBorder)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TextKey("Step.SmartBpFullBpFlowBasic.0.Description")
                    .TargetName(nameof(SmartBpStartFullBpFlowButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PostGameAutoFill)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    LinesKey = "Dialogue.SmartBpPostGameAutoFill.Lines"
                })
            .Build();
    }
}
