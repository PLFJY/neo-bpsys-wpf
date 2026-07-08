using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in tutorial packages.
/// </summary>
public static class NeoBpsysTutorialPackages
{
    /// <summary>
    /// Registers package definitions.
    /// </summary>
    /// <param name="packageRegistry">Package registry.</param>
    public static void Register(ITutorialPackageRegistry packageRegistry)
    {
        foreach (var package in CreatePackages())
        {
            packageRegistry.Register(package);
        }
    }

    /// <summary>
    /// Creates all built-in package definitions.
    /// </summary>
    /// <returns>Package definitions.</returns>
    public static IReadOnlyList<TutorialPackageDefinition> CreatePackages()
    {
        var packages = new List<TutorialPackageDefinition>();
        var registeredPackageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (pageKey, packageIds) in NeoBpsysTutorialSequences.GetSequences())
        {
            for (var i = 0; i < packageIds.Length; i++)
            {
                packages.Add(CreatePackage(pageKey, packageIds[i], i + 1));
                registeredPackageIds.Add(packageIds[i]);
            }
        }

        foreach (var packageId in NeoBpsysTutorialFlows.FirstRunIncludedPackages)
        {
            if (registeredPackageIds.Add(packageId))
            {
                packages.Add(CreatePackage(GetStandalonePackagePageKey(packageId), packageId, 0));
            }
        }

        return packages;
    }

    private static TutorialPackageDefinition CreatePackage(string pageKey, string packageId, int sequence)
    {
        var builder = TutorialPackageBuilder.Create(packageId)
            .ForPage(pageKey)
            .Version(1)
            .Sequence(sequence)
            .Kind("ProductTour");

        if (IsSmartBpModuleShellPackage(packageId))
        {
            builder.CanRun(IsSmartBpModuleNotLoaded);
        }
        else if (packageId == TutorialPackageIds.SmartBpPostGameAutoFill)
        {
            builder.CanRun(IsSmartBpPostGameAutoFillVisible);
        }
        else if (IsSmartBpModuleContentPackage(packageId))
        {
            builder.CanRun(IsSmartBpModuleLoaded);
        }

        foreach (var step in CreateSteps(packageId))
        {
            builder.AddStep(step);
        }

        return builder.Build();
    }

    private static IReadOnlyList<ProductTourStep> CreateSteps(string packageId)
    {
        return packageId switch
        {
            TutorialPackageIds.FrontManageBpWindowLaunchBasic =>
            [
                ElementTagStep(
                    FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow),
                    "启动 BP 前台窗口",
                    "导播时，观众看到的是前台窗口。我们先只启动 BP 前台页面。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.BpWindowOpened)
            ],
            TutorialPackageIds.FrontManageOverview =>
            [
                Step(
                    TutorialTargetNames.FrontManageTabs,
                    "前台管理",
                    "这里是前台管理页面。前台管理分为“前台窗口”和“布局包”两个区域。前台窗口负责打开、关闭观众看到的窗口。布局包负责导入、切换和管理前台界面方案。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.FrontManageWindowsBasic =>
            [
                Step(
                    TutorialTargetNames.FrontedWindowsTab,
                    "前台窗口",
                    "这里管理所有前台窗口。导播排查和控制窗口时，可以在这里查看窗口列表。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.OpenFrontedDesignerButton,
                    "打开设计器",
                    "点击这里可以打开 v3 前台设计器，用来编辑前台布局、控件属性和动画行为。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.OpenAllFrontedWindowsButton,
                    "打开全部窗口",
                    "“打开全部”会一次性打开所有前台窗口，适合导播开播前检查 OBS 捕获来源。它可能同时弹出多个窗口，本教程不会要求你必须点击。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.CloseAllFrontedWindowsButton,
                    "关闭全部窗口",
                    "“关闭全部”会一次性关闭所有前台窗口，适合导播收尾或排查窗口状态。本教程不会等待关闭操作完成。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.StopAllLoopAnimationsButton,
                    "停止循环动画",
                    "如果前台循环动画异常，可以使用“停止所有循环动画”。这是导播排查动画状态时的控制功能。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.ManageableWindowGroupsPanel,
                    "窗口分组",
                    "这里按分组列出可管理的前台窗口。每个窗口都可以单独打开或关闭。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.FirstManageableWindowCard,
                    "单个窗口",
                    "单个窗口卡片可以独立打开或关闭对应前台窗口。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.FrontManageOpenDesigner =>
            [
                Step(
                    TutorialTargetNames.OpenFrontedDesignerButton,
                    "打开 v3 编辑器",
                    "v3 编辑器用于编辑前台布局、控件属性和动画行为。点击这里可以打开前台设计器。打开后会进入独立的 v3 编辑器教程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.DesignerV3Opened,
                    allowMissing: true)
            ],
            TutorialPackageIds.FrontManageLayoutPackagesBasic =>
            [
                Step(
                    TutorialTargetNames.LayoutPackagesTab,
                    "布局包",
                    "布局包是前台界面的打包格式。可以导入别人制作的布局包，也可以切换当前启用的布局包。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.LayoutPackageList,
                    "布局包列表",
                    "这里显示已安装的布局包。旧版 bpui 包会尝试转换，部分布局包可能依赖插件。内置布局无法被直接修改。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.ImportLayoutPackageButton,
                    "导入布局包",
                    "点击导入会打开文件选择器。本教程不要求你选择文件；导入和切换布局包前，建议确认来源和兼容性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.ActivateLayoutPackageButton,
                    "启用布局包",
                    "点击启用可以切换当前前台界面方案。切换前请确认布局包来源、插件依赖和兼容性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.ActiveLayoutPackagePanel,
                    "当前布局包",
                    "这里显示当前启用的布局包和管理状态。如果你编辑内置布局，系统会自动切换到一个新的用户自定义布局，避免覆盖内置方案。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.DesignerV3Overview =>
            [
                Step(
                    null,
                    "欢迎来到 v3 编辑器",
                    "在这里你可以详细修改前台界面，包括布局位置、控件属性、图片资源和动画行为。",
                    ProductTourInteractionMode.BlockAll),
                Step(
                    TutorialTargetNames.DesignerToolbarHost,
                    "v3 前台设计器",
                    "这是 v3 前台设计器。顶部是保存、导入、导出、缩放等工具。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.LayerPanelHostGrid,
                    "图层区域",
                    "左侧是控件和图层列表。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.PreviewWorkspace,
                    "布局预览",
                    "中间是布局预览区域。你可以在这里观察前台最终效果。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.PropertyPanelHost,
                    "属性区域",
                    "右侧是属性编辑区域。稍后在预览区域选中控件后，这里会显示可编辑属性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.DesignerV3LayoutEditBasic =>
            [
                Step(
                    TutorialTargetNames.LayerPanelHostGrid,
                    "图层列表",
                    "图层列表显示当前布局里的控件。图层顺序会影响控件显示层级。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.LayerPanelScrollViewer,
                    "调整图层顺序",
                    "可以通过拖拽调整图层顺序。本教程不强制你拖拽图层。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.PreviewZoomHost,
                    "预览画布",
                    "中间画布显示最终前台效果。请试着点击画布上的一个控件来选中它；如果当前没有布局控件，也可以直接继续。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.InteractionLayer,
                    "交互层",
                    "交互层负责选中、拖动和缩放设计控件。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.DesignerV3PropertyPanelBasic =>
            [
                Step(
                    TutorialTargetNames.PropertyPanelHost,
                    "属性面板",
                    "选中控件后，右侧会显示属性。文本、颜色、图片、字体等都在这里修改。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.PropertySearchBox,
                    "属性筛选",
                    "这里可以筛选属性。如果没有选中控件，属性面板可能为空。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.FirstEditablePropertyEditor,
                    "属性编辑器",
                    "不同属性会显示不同编辑器。有些属性需要点击确认按钮才会应用。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.PropertyApplyButton,
                    "应用属性",
                    "如果属性非法，会显示错误状态。本教程不强制你修改或应用属性。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.DesignerV3BehaviorEditBasic =>
            [
                Step(
                    TutorialTargetNames.BehaviorPanelHost,
                    "动画行为",
                    "动画行为由“触发条件”和“动作”组成，可以让控件在特定状态下自动出现、隐藏、移动或循环。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.AddBehaviorButton,
                    "新增行为",
                    "这里可以新增显示、隐藏、移动、透明度变化或循环动画。本教程不强制创建行为。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.BehaviorTriggerEditor,
                    "触发条件",
                    "触发条件决定动画什么时候运行。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.BehaviorActionEditor,
                    "动作编辑",
                    "动作决定控件如何变化。如果动画异常循环，可以回到前台管理页面停止所有循环动画。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.DesignerV3PackageImportExport =>
            [
                Step(
                    TutorialTargetNames.SaveLayoutButton,
                    "保存布局",
                    "修改布局后需要保存。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.ExportLayoutPackageButton,
                    "导出布局包",
                    "可以将布局导出为布局包。当前版本的布局包导出入口在前台管理页面的布局包区域。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.ImportLayoutPackageButton,
                    "导入布局包",
                    "布局包也可以在前台管理页面导入和启用。如果布局包依赖插件，需要先安装对应插件。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.DesignerHelpButton,
                    "查看详细说明",
                    "右下角这个按钮可以打开 v3 编辑器的详细说明。遇到属性、行为或布局包规则不清楚时，可以点击这里查看。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.SmartBpModuleShell =>
            [
                Step(
                    TutorialTargetNames.SmartBpModulePathTextBox,
                    "模块路径",
                    "SmartBP 是独立模块，需要先加载模块。这里显示当前模块路径。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpModuleBrowseFolderButton,
                    "浏览文件夹",
                    "可以浏览文件夹选择模块路径。本教程不要求你完成文件选择。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpLoadLocalModuleButton,
                    "加载本地模块",
                    "可以加载本地模块。本教程不强制你真的加载模块。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpSelectInstalledModulePathButton,
                    "选择安装目录",
                    "如果已经安装模块，可以选择安装目录。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpImportModuleArchiveButton,
                    "导入模块压缩包",
                    "也可以导入 SmartBpModule.7z 或 .zip。本教程不等待文件选择器完成。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.SmartBpModuleContentOverview =>
            [
                Step(
                    TutorialTargetNames.SmartBpModuleContentHost,
                    "SmartBP 模块内容",
                    "SmartBP 用于识别游戏画面并辅助填写 BP 和赛后数据。使用顺序是：捕获窗口 -> 配置识别区域 -> 预览确认 -> 启动识别。它不是替代人工导播，而是辅助减少重复操作。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.SmartBpCaptureBasic =>
            [
                Step(
                    TutorialTargetNames.SmartBpWindowSelector,
                    "选择游戏窗口",
                    "先选择第五人格游戏窗口。第五人格游戏进程通常是 dwrg.exe。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpStartCaptureButton,
                    "开始捕获",
                    "选择窗口后，SmartBP 可以捕获游戏画面。本教程不强制开始捕获，也不会等待捕获成功。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpPreviewPanel,
                    "预览确认",
                    "预览区域用于确认捕获是否正确。如果没有找到 dwrg.exe，也可以继续教程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpStopCaptureButton,
                    "停止捕获",
                    "如果捕获异常，可以停止捕获。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.SmartBpRegionEditorBasic =>
            [
                Step(
                    TutorialTargetNames.SmartBpRegionEditorButton,
                    "识别区域",
                    "识别区域决定 AI / OCR 看哪里。不同阶段有不同区域，例如 Ban 求生、Ban 监管、Pick、赛后数据。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpRegionPreviewPanel,
                    "区域预览",
                    "如果识别不准，优先检查识别区域是否对齐。可以通过预览画面调整区域。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpRegionListPanel,
                    "区域列表",
                    "这里列出可配置的识别区域。本教程不强制拖拽区域。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpSaveRegionButton,
                    "保存区域",
                    "保存区域后再开始识别。本教程不强制保存，也不等待保存完成。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.SmartBpFullBpFlowBasic =>
            [
                Step(
                    TutorialTargetNames.SmartBpStartFullBpFlowButton,
                    "全流程 BP",
                    "全流程 BP 会根据当前比赛阶段自动识别。启动前请确认窗口捕获和识别区域正确。正式比赛中建议先预览确认，再启动。点击后会开始 SmartBP 的自动 BP 流程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.SmartBpPostGameAutoFill =>
            [
                Step(
                    TutorialTargetNames.SmartBpPostGameDataButton,
                    "赛后数据识别",
                    "对局结束后切到赛后数据页面。SmartBP 可以识别赛后数据。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpPostGamePreviewPanel,
                    "赛后数据预览",
                    "识别结果可用于填写对局数据。如果识别不对，可以手动修正。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.SmartBpPostGameApplyButton,
                    "应用识别结果",
                    "使用前请确认赛后数据区域配置正确。本教程不强制识别或应用结果。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.GameManageGameProgressBo1FirstHalf or TutorialPackageIds.GameManageBasic =>
            [
                Step(
                    TutorialTargetNames.GameProgressComboBox,
                    "选择场次",
                    "现在选择本次教学使用的场次。我们先从 BO1 上半开始。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
            ],
            TutorialPackageIds.GameManageNewGameBasic =>
            [
                Step(
                    TutorialTargetNames.NewGameButton,
                    "新建对局",
                    "新建对局会清空当前局的选择结果，但会保留全局禁选记录。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NewGameCreated)
            ],
            TutorialPackageIds.MainNavigationBasic =>
            [
                NavigationStep(
                    typeof(TeamInfoPage).FullName!,
                    "进入队伍管理",
                    "先进入队伍管理页面，我们会设置本次教学使用的队伍。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationTeamInfoOpened)
            ],
            TutorialPackageIds.MainNavigationFrontManage =>
            [
                NavigationStep(
                    typeof(FrontManagePage).FullName!,
                    "进入前台管理",
                    "先进入前台管理页面，打开 BP 前台窗口供 OBS 捕获。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationFrontManageOpened)
            ],
            TutorialPackageIds.MainNavigationTeamInfo =>
            [
                NavigationStep(
                    typeof(TeamInfoPage).FullName!,
                    "进入队伍管理",
                    "进入队伍管理页面，设置教学使用的队伍信息。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationTeamInfoOpened)
            ],
            TutorialPackageIds.MainNavigationScore =>
            [
                NavigationStep(
                    typeof(ScorePage).FullName!,
                    "进入比分页面",
                    "进入比分页面，选择当前半场的比分结果。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationScoreOpened)
            ],
            TutorialPackageIds.MainNavigationSmartBp =>
            [
                NavigationStep(
                    typeof(SmartBpPage).FullName!,
                    "进入智慧 BP",
                    "智慧 BP 是独立模块，首次进入后会有单独教程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationSmartBpOpened)
            ],
            TutorialPackageIds.MainNavigationDesignerV3 =>
            [
                Step(
                    null,
                    "前台界面编辑",
                    "前台界面编辑、布局编辑和动画行为编辑，会在首次打开 v3 编辑器时单独教学。",
                    ProductTourInteractionMode.BlockAll)
            ],
            TutorialPackageIds.TeamInfoTeamNameBasic or TutorialPackageIds.TeamInfoBasic =>
            [
                Step(
                    TutorialTargetNames.HomeTeamNameInput,
                    "填写队伍名称",
                    "这里可以设置队伍名称。先试着输入一个队伍名。",
                    ProductTourInteractionMode.AllowTargetOnly),
                Step(
                    TutorialTargetNames.HomeTeamNameConfirmButton,
                    "确认队伍名称",
                    "点击确认后，队伍名称会写入当前比赛数据。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.TeamNameConfirmed),
                Step(
                    TutorialTargetNames.HomeTeamLogoButton,
                    "设置队伍 Logo",
                    "这里可以设置主队 Logo。本次导览可以直接点击下一步继续。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.MainTeamSummaryBasic =>
            [
                Step(
                    TutorialTargetNames.TeamSummaryCard,
                    "确认队伍信息",
                    "队伍名已经显示在 MainWindow 上方功能区。这里也可以进行换边。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ],
            TutorialPackageIds.TeamInfoJsonImportPreset or TutorialPackageIds.TeamInfoJsonImport =>
            [
                Step(
                    TutorialTargetNames.HomeTeamJsonImportButton,
                    "导入狼队预设",
                    "点击导入后，在打开的文件对话框中选择“队伍信息导入示例-Wolves.json”。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    beforeShowAsync: (_, _) =>
                    {
                        SetExamplesJsonPickerHint("请导入狼队信息：选择“队伍信息导入示例-Wolves.json”");
                        return Task.CompletedTask;
                    }),
                Step(
                    TutorialTargetNames.HomePlayerListPanel,
                    "调整狼队上场下场",
                    "导入后，在这里调整狼队成员的上场和下场状态。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.BottomRight,
                    avatarPose: TutorialAvatarPose.LeftTop),
                Step(
                    TutorialTargetNames.AwayTeamJsonImportButton,
                    "导入 GR 预设",
                    "点击导入后，在打开的文件对话框中选择“队伍信息导入示例-GR.json”。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    scrollAnchorName: TutorialTargetNames.AwayTeamInfoCard,
                    beforeShowAsync: (_, _) =>
                    {
                        SetExamplesJsonPickerHint("请导入 GR 信息：选择“队伍信息导入示例-GR.json”");
                        return Task.CompletedTask;
                    }),
                Step(
                    TutorialTargetNames.AwayPlayerListPanel,
                    "调整 GR 上场下场",
                    "导入后，在这里调整 GR 成员的上场和下场状态。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.BottomRight,
                    avatarPose: TutorialAvatarPose.LeftTop)
            ],
            TutorialPackageIds.TeamInfoPlayerManage =>
            [
                Step(
                    TutorialTargetNames.HomePlayerPositionPanel,
                    "调整队伍成员顺序",
                    "这里可以调整当前上场队员的顺序，前台和 BP 流程会使用这些信息。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.BottomRight,
                    avatarPose: TutorialAvatarPose.LeftTop)
            ],
            TutorialPackageIds.BpGameGuidanceStartBasic or TutorialPackageIds.BpGameGuidanceBasic =>
            [
                Step(
                    TutorialTargetNames.StartGameGuidanceButton,
                    "开启对局引导",
                    "对局引导会按照当前场次，带你完成地图、Ban/Pick 和后续流程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameGuidanceStarted)
            ],
            TutorialPackageIds.MapBpCompletionNextBasic or TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf =>
            [
                Step(
                    TutorialTargetNames.NextGuidanceStepButton,
                    "进入下一阶段",
                    "当前阶段已经完成，点击下一步进入角色 BP。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GuidanceNextClicked)
            ],
            TutorialPackageIds.BpCharacterSelectorBasic =>
            [
                DescendantTypeStep(
                    TutorialTargetNames.FirstBanSurvivorSelectorHost,
                    typeof(CharacterSelector).FullName!,
                    "先按空格匹配角色",
                    "这是角色选择器，不是普通下拉框。请先输入一个角色的全称、拼音全拼或简拼，然后按空格触发匹配。这一步先不要点确认。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSearchCommitted,
                    allowMissing: true),
                DescendantTypeStep(
                    TutorialTargetNames.FirstBanSurvivorSelectorHost,
                    typeof(CharacterSelector).FullName!,
                    "确认角色选择",
                    "匹配到角色后，再按 Enter / Tab 或点击确认按钮完成选择。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSelectionConfirmed,
                    allowMissing: true)
            ],
            TutorialPackageIds.BpPickCharacterBasic =>
            [
                DescendantTypeStep(
                    TutorialTargetNames.FirstSurvivorPickSelectorHost,
                    typeof(CharacterSelector).FullName!,
                    "选择 1、2 号角色",
                    "继续在 Pick 页面选择 1、2 号求生者角色，选择结果会记录到全局禁选中。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSelectionConfirmed,
                    allowMissing: true)
            ],
            TutorialPackageIds.BpGlobalBanRecordBasic =>
            [
                ElementTagStep(
                    TutorialTargetNames.CurrentSurvivorGlobalBanRecordPanel,
                    "全局禁选记录",
                    "刚刚的选择已经被记录到全局禁选中。全局禁选会影响后续场次，新建对局会清空当局选择但保留这些记录。",
                    ProductTourInteractionMode.AllowAll,
                    allowMissing: true,
                    cardOffset: new Point(80, 0))
            ],
            TutorialPackageIds.ScoreBasic =>
            [
                Step(
                    TutorialTargetNames.ScoreSelectorPanel,
                    "选择比分",
                    "对局结束后，可以在这里选择比分。比分会同步到比赛状态和前台显示。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.ScoreChanged,
                    allowMissing: true)
            ],
            TutorialPackageIds.GameManageGlobalBanCarryOver =>
            [
                Step(
                    null,
                    "全局禁选继承",
                    "切换场次后，再次开启对局引导时，软件会自动导入之前的全局禁选记录，并设置对应 Ban 位。",
                    ProductTourInteractionMode.BlockAll)
            ],
            _ =>
            [
                Step(
                    null,
                    "功能教学",
                    NeoBpsysTutorialTexts.GetFallbackDescription(packageId),
                    ProductTourInteractionMode.BlockAll)
            ]
        };
    }

    private static ProductTourStep NavigationStep(
        string targetPageTypeFullName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .StepNavigationItem(targetPageTypeFullName)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Right)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        var step = builder.EndStep().Build().Steps[0];
        if (signalId != null)
        {
            step.AfterCompleteAsync = DelayForNavigationTransitionAsync;
        }

        return step;
    }

    private static ProductTourStep Step(
        string? targetName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false,
        Func<IServiceProvider, CancellationToken, Task>? beforeShowAsync = null,
        ProductTourAvatarPlacement avatarPlacement = ProductTourAvatarPlacement.Auto,
        TutorialAvatarPose? avatarPose = null,
        Point? cardOffset = null,
        string? scrollAnchorName = null)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .Step(targetName)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Auto)
            .CardOffset(cardOffset ?? default)
            .AvatarPlacement(avatarPlacement)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (!string.IsNullOrWhiteSpace(scrollAnchorName))
        {
            builder.ScrollAnchor(scrollAnchorName);
        }

        if (avatarPose != null)
        {
            builder.AvatarPose(avatarPose.Value);
        }

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        var step = builder.EndStep().Build().Steps[0];
        step.BeforeShowAsync = beforeShowAsync;
        return step;
    }

    private static ProductTourStep DescendantTypeStep(
        string? hostTargetName,
        string targetTypeFullName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .StepDescendantType(hostTargetName, targetTypeFullName)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Auto)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        return builder.EndStep().Build().Steps[0];
    }

    private static ProductTourStep ElementTagStep(
        string targetTag,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false,
        Point? cardOffset = null,
        Func<IServiceProvider, CancellationToken, Task>? beforeShowAsync = null)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .StepElementTag(targetTag)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Auto)
            .CardOffset(cardOffset ?? default)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        var step = builder.EndStep().Build().Steps[0];
        step.BeforeShowAsync = beforeShowAsync;
        return step;
    }

    private static void SetExamplesJsonPickerHint(string title)
    {
        TutorialFilePickerHints.SetNextJsonPickerHint(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples"),
            title);
    }

    private static bool IsSmartBpModuleShellPackage(string packageId) =>
        packageId == TutorialPackageIds.SmartBpModuleShell;

    private static bool IsSmartBpModuleContentPackage(string packageId) =>
        packageId is TutorialPackageIds.SmartBpModuleContentOverview
            or TutorialPackageIds.SmartBpCaptureBasic
            or TutorialPackageIds.SmartBpRegionEditorBasic
            or TutorialPackageIds.SmartBpFullBpFlowBasic;

    private static string GetStandalonePackagePageKey(string packageId) =>
        packageId switch
        {
            TutorialPackageIds.FrontManageBpWindowLaunchBasic => TutorialPageKeys.FrontManage,
            _ => TutorialPageKeys.Main
        };

    private static bool IsSmartBpModuleLoaded(IServiceProvider serviceProvider) =>
        serviceProvider.GetService(typeof(SmartBpPageViewModel)) is SmartBpPageViewModel viewModel
            && viewModel.IsModuleLoaded;

    private static bool IsSmartBpModuleNotLoaded(IServiceProvider serviceProvider) =>
        serviceProvider.GetService(typeof(SmartBpPageViewModel)) is not SmartBpPageViewModel viewModel
            || !viewModel.IsModuleLoaded;

    private static bool IsSmartBpPostGameAutoFillVisible(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
        return false;
    }

    private static Task DelayForNavigationTransitionAsync(IServiceProvider _, CancellationToken cancellationToken) =>
        Task.Delay(450, cancellationToken);
}
