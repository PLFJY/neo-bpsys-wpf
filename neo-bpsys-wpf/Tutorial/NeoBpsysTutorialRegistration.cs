using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in neo-bpsys-wpf tutorial packages and flows.
/// </summary>
public static class NeoBpsysTutorialRegistration
{
    private static readonly (string PageKey, string[] Packages)[] PageSequences =
    [
        ("Page.TeamInfo", ["Page.TeamInfo.Basic", "Page.TeamInfo.JsonImport", "Page.TeamInfo.PlayerManage", "Page.TeamInfo.Advanced"]),
        ("Page.Bp.Shared", ["Page.Bp.Shared.Basic", "Page.Bp.CharacterSelector.Basic", "Page.Bp.GlobalBanRecord.Basic", "Page.Bp.CharacterSelector.Advanced"]),
        ("Page.Bp.GameGuidance", ["Page.Bp.GameGuidance.Basic", "Page.Bp.GameGuidance.FlowBo1FirstHalf"]),
        ("Page.Score", ["Page.Score.Basic", "Page.Score.FrontedSync", "Page.Score.Advanced"]),
        ("Page.GameManage", ["Page.GameManage.Basic", "Page.GameManage.ImportExport", "Page.GameManage.GlobalBanCarryOver"]),
        ("Page.FrontManage", ["Page.FrontManage.BpWindowLaunch.Basic", "Page.FrontManage.Windows.Basic", "Page.FrontManage.LayoutPackages.Basic", "Page.FrontManage.Advanced"]),
        ("Window.DesignerV3", ["Window.DesignerV3.LayoutEdit.Basic", "Window.DesignerV3.BehaviorEdit.Basic", "Window.DesignerV3.PackageImportExport", "Window.DesignerV3.Advanced"]),
        ("Page.SmartBp", ["Page.SmartBp.ModuleShell", "Page.SmartBp.Capture.Basic", "Page.SmartBp.RegionEditor.Basic", "Page.SmartBp.FullBpFlow.Basic", "Page.SmartBp.PostGameAutoFill"])
    ];

    private static readonly string[] FirstRunIncludedPackages =
    [
        "Page.FrontManage.BpWindowLaunch.Basic",
        "Page.GameManage.Basic",
        "Page.TeamInfo.Basic",
        "Page.TeamInfo.JsonImport",
        "Page.TeamInfo.PlayerManage",
        "Page.Bp.GameGuidance.Basic",
        "Page.Bp.GameGuidance.FlowBo1FirstHalf",
        "Page.Bp.Shared.Basic",
        "Page.Bp.CharacterSelector.Basic",
        "Page.Bp.GlobalBanRecord.Basic",
        "Page.Score.Basic",
        "Page.GameManage.GlobalBanCarryOver"
    ];

    /// <summary>
    /// Registers all built-in tutorial definitions.
    /// </summary>
    /// <param name="packageRegistry">Package registry.</param>
    /// <param name="sequenceRegistry">Sequence registry.</param>
    /// <param name="flowRegistry">Flow registry.</param>
    public static void Register(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry)
    {
        foreach (var (pageKey, packages) in PageSequences)
        {
            sequenceRegistry.RegisterSequence(pageKey, packages);
            for (var i = 0; i < packages.Length; i++)
            {
                packageRegistry.Register(CreatePackage(pageKey, packages[i], i + 1));
            }
        }

        flowRegistry.Register(new TutorialFlowDefinition
        {
            FlowId = "Flow.FirstRun.StandardBp",
            Version = 1,
            IncludedPackageIds = FirstRunIncludedPackages,
            Items =
            [
                new DialogueFlowItem
                {
                    Lines =
                    [
                        "欢迎来到 neo-bpsys-wpf。",
                        "现在我们来进行一下简单的导播教学。"
                    ]
                },
                ..FirstRunIncludedPackages.Select(id => new PackageFlowItem { PackageId = id }),
                new DialogueFlowItem
                {
                    Lines =
                    [
                        "前台界面编辑、布局包导入导出和动画行为编辑，会在你首次打开 v3 编辑器时单独教学。",
                        "智慧 BP 是独立模块，首次进入后会有窗口捕获、识别区域和自动识别教程。",
                        "开始你的导播之旅吧。"
                    ]
                }
            ]
        });
    }

    private static TutorialPackageDefinition CreatePackage(string pageKey, string packageId, int sequence)
    {
        return new TutorialPackageDefinition
        {
            PackageId = packageId,
            PageKey = pageKey,
            Sequence = sequence,
            Version = 1,
            Kind = "ProductTour",
            Steps = CreateSteps(packageId)
        };
    }

    private static IReadOnlyList<ProductTourStep> CreateSteps(string packageId)
    {
        return packageId switch
        {
            "Page.FrontManage.BpWindowLaunch.Basic" =>
            [
                Step("BpWindowLaunchButton", "启动 BP 前台窗口", "导播时，观众看到的是前台窗口。我们先启动 BP 前台页面。", ProductTourInteractionMode.AllowTargetOnly, "BpWindowOpened")
            ],
            "Page.GameManage.Basic" =>
            [
                Step("GameProgressComboBox", "选择场次", "现在选择本次教学使用的场次。我们先从 BO1 上半开始。", ProductTourInteractionMode.AllowTargetOnly, "GameProgressSelected.Bo1FirstHalf")
            ],
            "Page.TeamInfo.Basic" =>
            [
                Step("TeamNameInput", "填写队伍名称", "这里可以设置队伍名称。先试着输入一个队伍名。", ProductTourInteractionMode.AllowTargetOnly, "TeamNameConfirmed", allowMissing: true),
                Step("TeamSummaryCard", "确认队伍信息", "队伍名已经显示在这里。比赛中也可以在这里快速换边。", ProductTourInteractionMode.BlockAll, allowMissing: true)
            ],
            "Page.TeamInfo.JsonImport" =>
            [
                Step("ImportTeamJsonButton", "导入队伍 JSON", "如果已经准备好队伍 JSON，可以直接导入。现在我们导入两支示例队伍。", ProductTourInteractionMode.AllowTargetOnly, "TeamJsonImported.Home", allowMissing: true)
            ],
            "Page.TeamInfo.PlayerManage" =>
            [
                Step("PlayerList", "管理选手", "这里可以管理选手上下场。", ProductTourInteractionMode.AllowTargetOnly, "MemberStateChanged", allowMissing: true),
                Step("PlayerPositionPanel", "调整位置", "这里可以调整选手位置，前台和 BP 流程会使用这些信息。", ProductTourInteractionMode.AllowTargetOnly, "MemberPositionSwapped", allowMissing: true)
            ],
            "Page.Bp.GameGuidance.Basic" =>
            [
                Step("StartGameGuidanceButton", "开启对局引导", "对局引导会按照当前场次，带你完成地图、Ban/Pick 和后续流程。", ProductTourInteractionMode.AllowTargetOnly, "GameGuidanceStarted")
            ],
            "Page.Bp.GameGuidance.FlowBo1FirstHalf" =>
            [
                Step("NextGuidanceStepButton", "进入下一阶段", "当前阶段已经完成，点击下一步进入角色 BP。", ProductTourInteractionMode.AllowTargetOnly, "GuidanceNextClicked")
            ],
            "Page.Bp.CharacterSelector.Basic" =>
            [
                Step("CharacterSelector", "角色选择器", "这是角色选择器，不是普通下拉框。它支持角色名、拼音全拼和缩写搜索。输入后按空格可以搜索。按 Enter / Tab 或点击确认按钮完成选择。", ProductTourInteractionMode.AllowTargetOnly, "CharacterSelector.SelectionConfirmed", allowMissing: true)
            ],
            "Page.Bp.GlobalBanRecord.Basic" =>
            [
                Step("GlobalBanRecordPanel", "全局禁选记录", "全局禁选会影响后续场次。新建下一局时，当局选择会被清空，但这些全局记录会被保留。", ProductTourInteractionMode.BlockAll, "GlobalBanRecordUpdated", allowMissing: true)
            ],
            "Page.Score.Basic" =>
            [
                Step("ScoreSelectorPanel", "选择比分", "对局结束后，可以在这里选择比分。比分会同步到比赛状态和前台显示。", ProductTourInteractionMode.AllowTargetOnly, "ScoreChanged", allowMissing: true)
            ],
            "Page.GameManage.GlobalBanCarryOver" =>
            [
                Step("NewGameButton", "新建对局", "新建对局会清空当前局的选择结果，但会保留全局禁选。", ProductTourInteractionMode.AllowTargetOnly, "NewGameCreated"),
                Step(null, "全局禁选继承", "切换场次后，再次开启对局引导时，软件会自动导入之前的全局禁选记录，并设置对应 Ban 位。", ProductTourInteractionMode.BlockAll)
            ],
            _ =>
            [
                Step(null, "功能教学", GetFallbackDescription(packageId), ProductTourInteractionMode.BlockAll)
            ]
        };
    }

    private static ProductTourStep Step(
        string? targetName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false)
    {
        return new ProductTourStep
        {
            TargetName = targetName,
            Title = title,
            Description = description,
            Placement = ProductTourPlacement.Auto,
            InteractionMode = mode,
            WaitForSignalId = signalId,
            ExpectedAction = signalId == null ? TutorialExpectedAction.None : TutorialExpectedAction.SignalReceived,
            AllowMissingTarget = allowMissing,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static string GetFallbackDescription(string packageId)
    {
        return packageId switch
        {
            "Page.Score.FrontedSync" => "比分会同步到比赛状态和前台显示。",
            "Page.FrontManage.Windows.Basic" => "这里可以管理前台窗口的打开、关闭和输出状态。",
            "Page.FrontManage.LayoutPackages.Basic" => "这里可以导入、导出和切换前台布局包。",
            "Window.DesignerV3.LayoutEdit.Basic" => "这里可以编辑 v3 前台窗口布局。",
            "Window.DesignerV3.BehaviorEdit.Basic" => "这里可以编辑前台窗口动画行为。",
            "Page.SmartBp.ModuleShell" => "智慧 BP 是独立模块，首次进入后会提供捕获、识别区域和自动识别教程。",
            _ => "这个功能的详细教学将在你首次进入对应页面时提供。"
        };
    }
}
