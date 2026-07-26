using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Views.Pages;

public partial class SmartBpModuleContentView : ITutorialOwner<SmartBpModuleContentView>
{
    /// <summary>SmartBP 模块内容教程键。</summary>
    public const string TutorialPageKey = "Page.SmartBp.ModuleContent";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>SmartBP 模块内容教程包 id。</summary>
    public static class PackageIds
    {
        /// <summary>SmartBP 模块内容概览包 id。</summary>
        public const string ModuleContentOverview = "Page.SmartBp.ModuleContent.Overview";

        /// <summary>SmartBP OCR 模型下载与管理包 id。</summary>
        public const string OcrModelDownloadBasic = "Page.SmartBp.OcrModelDownload.Basic";

        /// <summary>SmartBP 捕获包 id。</summary>
        public const string CaptureBasic = "Page.SmartBp.Capture.Basic";

        /// <summary>SmartBP 区域编辑器入口包 id。</summary>
        public const string RegionEditorEntryBasic = "Page.SmartBp.RegionEditorEntry.Basic";

        /// <summary>SmartBP 完整 BP 流程包 id。</summary>
        public const string FullBpFlowBasic = "Page.SmartBp.FullBpFlow.Basic";

        /// <summary>SmartBP 赛后自动回填包 id。</summary>
        public const string PostGameAutoFill = "Page.SmartBp.PostGameAutoFill";
    }

    /// <summary>SmartBP 模块内容教程包引用。</summary>
    public static class Tours
    {
        /// <summary>SmartBP 模块内容概览包引用。</summary>
        public static readonly TutorialPackageRef ModuleContentOverview = new(PackageIds.ModuleContentOverview);

        /// <summary>SmartBP OCR 模型下载与管理包引用。</summary>
        public static readonly TutorialPackageRef OcrModelDownloadBasic = new(PackageIds.OcrModelDownloadBasic);

        /// <summary>SmartBP 捕获基础包引用。</summary>
        public static readonly TutorialPackageRef CaptureBasic = new(PackageIds.CaptureBasic);

        /// <summary>SmartBP 区域编辑器入口包引用。</summary>
        public static readonly TutorialPackageRef RegionEditorEntryBasic = new(PackageIds.RegionEditorEntryBasic);

        /// <summary>SmartBP 完整 BP 流程包引用。</summary>
        public static readonly TutorialPackageRef FullBpFlowBasic = new(PackageIds.FullBpFlowBasic);

        /// <summary>SmartBP 赛后自动回填包引用。</summary>
        public static readonly TutorialPackageRef PostGameAutoFill = new(PackageIds.PostGameAutoFill);
    }

    /// <summary>
    /// 注册由 SmartBP 模块内容视图拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
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
