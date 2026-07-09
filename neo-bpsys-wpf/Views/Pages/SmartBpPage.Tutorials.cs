using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Views.Pages;

public partial class SmartBpPage : ITutorialOwner<SmartBpPage>
{
    /// <summary>Smart BP page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.SmartBp;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Smart BP tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Smart BP module content overview package reference.</summary>
        public static readonly TutorialPackageRef ModuleContentOverview = new(TutorialPackageIds.SmartBpModuleContentOverview);

        /// <summary>Smart BP capture basic package reference.</summary>
        public static readonly TutorialPackageRef CaptureBasic = new(TutorialPackageIds.SmartBpCaptureBasic);

        /// <summary>Smart BP region editor package reference.</summary>
        public static readonly TutorialPackageRef RegionEditorBasic = new(TutorialPackageIds.SmartBpRegionEditorBasic);

        /// <summary>Smart BP full BP flow package reference.</summary>
        public static readonly TutorialPackageRef FullBpFlowBasic = new(TutorialPackageIds.SmartBpFullBpFlowBasic);

        /// <summary>Smart BP post-game auto-fill package reference.</summary>
        public static readonly TutorialPackageRef PostGameAutoFill = new(TutorialPackageIds.SmartBpPostGameAutoFill);
    }

    /// <summary>Smart BP tutorial target names from dynamically loaded module content.</summary>
    public static class TutorialTargets
    {
        /// <summary>Smart BP window selector target name.</summary>
        public const string WindowSelector = "SmartBpWindowSelector";

        /// <summary>Smart BP start capture button target name.</summary>
        public const string StartCaptureButton = "SmartBpStartCaptureButton";

        /// <summary>Smart BP preview panel target name.</summary>
        public const string PreviewPanel = "SmartBpPreviewPanel";

        /// <summary>Smart BP preview button target name.</summary>
        public const string PreviewButton = "SmartBpPreviewButton";

        /// <summary>Smart BP stop capture button target name.</summary>
        public const string StopCaptureButton = "SmartBpStopCaptureButton";

        /// <summary>Smart BP region editor button target name.</summary>
        public const string RegionEditorButton = "SmartBpRegionEditorButton";

        /// <summary>Smart BP region preview panel target name.</summary>
        public const string RegionPreviewPanel = "SmartBpRegionPreviewPanel";

        /// <summary>Smart BP region list panel target name.</summary>
        public const string RegionListPanel = "SmartBpRegionListPanel";

        /// <summary>Smart BP save region button target name.</summary>
        public const string SaveRegionButton = "SmartBpSaveRegionButton";

        /// <summary>Smart BP full BP flow start button target name.</summary>
        public const string StartFullBpFlowButton = "SmartBpStartFullBpFlowButton";

        /// <summary>Smart BP post-game data button target name.</summary>
        public const string PostGameDataButton = "SmartBpPostGameDataButton";

        /// <summary>Smart BP post-game preview panel target name.</summary>
        public const string PostGamePreviewPanel = "SmartBpPostGamePreviewPanel";

        /// <summary>Smart BP post-game apply button target name.</summary>
        public const string PostGameApplyButton = "SmartBpPostGameApplyButton";
    }

    /// <summary>
    /// Registers tutorials owned by the Smart BP page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<SmartBpPage>()
            .AutoRun(TutorialAutoRunStrategy.ContinueWhileActive)
            .Package(Tours.ModuleContentOverview)
                .CanRun(IsSmartBpModuleContentReady)
                .Step("SmartBP 模块内容")
                    .Text("SmartBP 用于识别游戏画面并辅助填写 BP 和赛后数据。使用顺序是：捕获窗口 -> 配置识别区域 -> 预览确认 -> 启动识别。它不是替代人工导播，而是辅助减少重复操作。")
                    .TargetName(nameof(SmartBpModuleContentHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.CaptureBasic)
                .CanRun(IsSmartBpCaptureReady)
                .Step("选择游戏窗口")
                    .Text("先选择第五人格游戏窗口。第五人格游戏进程通常是 dwrg.exe。")
                    .TargetName(TutorialTargets.WindowSelector)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("开始捕获")
                    .Text("选择窗口后，SmartBP 可以捕获游戏画面。本教程不强制开始捕获，也不会等待捕获成功。")
                    .TargetName(TutorialTargets.StartCaptureButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("预览捕获")
                    .Text("点击预览可以确认捕获画面是否正确。如果没有找到 dwrg.exe，也可以继续教程。")
                    .TargetName(TutorialTargets.PreviewButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("停止捕获")
                    .Text("如果捕获异常，可以停止捕获。")
                    .TargetName(TutorialTargets.StopCaptureButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.RegionEditorBasic)
                .CanRun(IsSmartBpRegionEditorReady)
                .Step("识别区域")
                    .Text("识别区域决定 AI / OCR 看哪里。不同阶段有不同区域，例如 Ban 求生、Ban 监管、Pick、赛后数据。")
                    .TargetName(TutorialTargets.RegionEditorButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("区域预览")
                    .Text("如果识别不准，优先检查识别区域是否对齐。可以通过预览画面调整区域。")
                    .TargetName(TutorialTargets.RegionPreviewPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("区域列表")
                    .Text("这里列出可配置的识别区域。本教程不强制拖拽区域。")
                    .TargetName(TutorialTargets.RegionListPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("保存区域")
                    .Text("保存区域后再开始识别。本教程不强制保存，也不等待保存完成。")
                    .TargetName(TutorialTargets.SaveRegionButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.FullBpFlowBasic)
                .CanRun(IsSmartBpFullBpFlowReady)
                .Step("全流程 BP")
                    .Text("全流程 BP 会根据当前比赛阶段自动识别。启动前请确认窗口捕获和识别区域正确。正式比赛中建议先预览确认，再启动。点击后会开始 SmartBP 的自动 BP 流程。")
                    .TargetName(TutorialTargets.StartFullBpFlowButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PostGameAutoFill)
                .CanRun(IsSmartBpPostGameAutoFillVisible)
                .Step("赛后数据识别")
                    .Text("对局结束后切到赛后数据页面。SmartBP 可以识别赛后数据。")
                    .TargetName(TutorialTargets.PostGameDataButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("赛后数据预览")
                    .Text("识别结果可用于填写对局数据。如果识别不对，可以手动修正。")
                    .TargetName(TutorialTargets.PostGamePreviewPanel)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("应用识别结果")
                    .Text("使用前请确认赛后数据区域配置正确。本教程不强制识别或应用结果。")
                    .TargetName(TutorialTargets.PostGameApplyButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();
    }

    private static bool IsSmartBpModuleLoaded(IServiceProvider serviceProvider, FrameworkElement? owner)
    {
        _ = serviceProvider;
        return owner is FrameworkElement { DataContext: SmartBpPageViewModel viewModel }
            && viewModel.IsModuleLoaded;
    }

    private static bool IsSmartBpModuleContentReady(IServiceProvider serviceProvider, FrameworkElement? owner)
    {
        _ = serviceProvider;
        return IsSmartBpModuleLoaded(serviceProvider, owner)
            && HasContentHostContent(owner);
    }

    private static bool IsSmartBpCaptureReady(IServiceProvider serviceProvider, FrameworkElement? owner) =>
        IsSmartBpModuleLoaded(serviceProvider, owner)
        && HasAnyTarget(
            owner,
            TutorialTargets.WindowSelector,
            TutorialTargets.StartCaptureButton);

    private static bool IsSmartBpRegionEditorReady(IServiceProvider serviceProvider, FrameworkElement? owner) =>
        IsSmartBpModuleLoaded(serviceProvider, owner)
        && HasAnyTarget(
            owner,
            TutorialTargets.RegionEditorButton,
            TutorialTargets.RegionPreviewPanel,
            TutorialTargets.RegionListPanel);

    private static bool IsSmartBpFullBpFlowReady(IServiceProvider serviceProvider, FrameworkElement? owner) =>
        IsSmartBpModuleLoaded(serviceProvider, owner)
        && HasAnyTarget(owner, TutorialTargets.StartFullBpFlowButton);

    private static bool IsSmartBpPostGameAutoFillVisible(IServiceProvider serviceProvider, FrameworkElement? owner)
    {
        _ = serviceProvider;
        _ = owner;
        return false;
    }

    private static bool HasContentHostContent(FrameworkElement? owner)
    {
        if (owner == null)
        {
            return false;
        }

        return FindNamedElement(owner, nameof(SmartBpModuleContentHost)) is ContentControl { Content: not null };
    }

    private static bool HasAnyTarget(FrameworkElement? owner, params string[] targetNames)
    {
        if (owner == null)
        {
            return false;
        }

        return targetNames.Any(targetName => FindNamedElement(owner, targetName) != null);
    }

    private static FrameworkElement? FindNamedElement(DependencyObject root, string targetName)
    {
        if (root is FrameworkElement element)
        {
            if (element.Name == targetName)
            {
                return element;
            }

            if (element.FindName(targetName) is FrameworkElement named)
            {
                return named;
            }
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var nested = FindNamedElement(VisualTreeHelper.GetChild(root, i), targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        if (root is ContentControl { Content: DependencyObject content })
        {
            return FindNamedElement(content, targetName);
        }

        return null;
    }
}
