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
                    Lines =
                    [
                        "智慧BP 用于识别游戏画面并辅助填写 BP 和赛后数据。",
                        "使用顺序是：下载 OCR 模型 -> 捕获窗口 -> 配置识别区域 -> 开始使用相关功能"
                    ]
                })
                .Step("识别模型管理")
                    .Text("智慧 BP 的 OCR 识别需要本地模型。在这里选择识别引擎，并检查、下载、更新或切换智慧 BP 使用的 OCR 模型。")
                    .TargetName(nameof(SmartBpOcrModelManagementCard))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.CaptureBasic)
                .Step("选择游戏窗口")
                    .Text("先选择第五人格游戏窗口。第五人格游戏进程通常是 dwrg.exe。")
                    .TargetName(nameof(SmartBpWindowSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("开始捕获")
                    .Text("选择窗口后，SmartBP 可以捕获游戏画面。本教程不强制开始捕获，也不会等待捕获成功。")
                    .TargetName(nameof(SmartBpStartCaptureButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("预览捕获")
                    .Text("点击预览可以确认捕获画面是否正确。如果没有找到 dwrg.exe，也可以继续教程。")
                    .TargetName(nameof(SmartBpPreviewButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("停止捕获")
                    .Text("如果捕获异常，可以停止捕获。")
                    .TargetName(nameof(SmartBpStopCaptureButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.RegionEditorEntryBasic)
                .Step("识别区域")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SmartBpPostGamePreviewPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .Text("识别区域决定 OCR 或视觉模型读取画面的哪些位置。不同阶段有不同区域，例如 Ban 求生、Ban 监管、Pick、赛后数据。点击此按钮可以打开识别区域编辑器。\nBP环节的识别区域")
                    .TargetName(nameof(SmartBpPostGamePreviewPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.FullBpFlowBasic)
                .Step("全流程 BP")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SmartBpStartFullBpFlowBorder)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .Text("全流程 BP 会根据当前比赛阶段自动识别。启动前请确认窗口捕获和识别区域正确。正式比赛中建议先预览确认，再启动。点击后会开始智慧 BP 的自动 BP 流程。")
                    .TargetName(nameof(SmartBpStartFullBpFlowButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PostGameAutoFill)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    Lines =
                    [
                        "对局结束后，智慧 BP 可以识别赛后数据并辅助填写。在赛后数据页面有“自动识别并填充赛后数据”的按钮",
                        "识别的赛后数据会自动匹配角色"
                    ]
                })
            .Build();
    }
}
