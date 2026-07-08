using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Views.Pages;

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
        foreach (var (pageKey, packageIds) in NeoBpsysTutorialSequences.GetSequences())
        {
            for (var i = 0; i < packageIds.Length; i++)
            {
                packages.Add(CreatePackage(pageKey, packageIds[i], i + 1));
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
            TutorialPackageIds.MainNavigationBasic =>
            [
                NavigationStep(
                    typeof(TeamInfoPage).FullName!,
                    "进入队伍管理",
                    "先进入队伍管理页面，我们会设置本次教学使用的队伍。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NavigationTeamInfoOpened)
            ],
            TutorialPackageIds.FrontManageBpWindowLaunchBasic =>
            [
                Step(
                    TutorialTargetNames.BpWindowLaunchButton,
                    "启动 BP 前台窗口",
                    "导播时，观众看到的是前台窗口。我们先启动 BP 前台页面。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.BpWindowOpened)
            ],
            TutorialPackageIds.GameManageBasic =>
            [
                Step(
                    TutorialTargetNames.GameProgressComboBox,
                    "选择场次",
                    "现在选择本次教学使用的场次。我们先从 BO1 上半开始。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameProgressSelectedBo1FirstHalf)
            ],
            TutorialPackageIds.TeamInfoBasic =>
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
                    TutorialTargetNames.TeamSummaryCard,
                    "确认队伍信息",
                    "队伍名已经显示在这里。比赛中也可以在这里快速换边。",
                    ProductTourInteractionMode.BlockAll,
                    allowMissing: true)
            ],
            TutorialPackageIds.TeamInfoJsonImport =>
            [
                Step(
                    TutorialTargetNames.HomeTeamJsonImportButton,
                    "导入队伍 JSON",
                    "如果已经准备好队伍 JSON，可以从这里导入。真实示例队伍会在教学沙盒完成后接入。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.TeamJsonImportedHome,
                    allowMissing: true)
            ],
            TutorialPackageIds.TeamInfoPlayerManage =>
            [
                Step(
                    TutorialTargetNames.HomePlayerListPanel,
                    "管理选手",
                    "这里可以管理主队选手信息和上下场状态。",
                    ProductTourInteractionMode.BlockAll,
                    allowMissing: true),
                Step(
                    TutorialTargetNames.HomePlayerPositionPanel,
                    "调整位置",
                    "这里可以调整选手位置，前台和 BP 流程会使用这些信息。",
                    ProductTourInteractionMode.BlockAll,
                    allowMissing: true)
            ],
            TutorialPackageIds.BpGameGuidanceBasic =>
            [
                Step(
                    TutorialTargetNames.StartGameGuidanceButton,
                    "开启对局引导",
                    "对局引导会按照当前场次，带你完成地图、Ban/Pick 和后续流程。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.GameGuidanceStarted)
            ],
            TutorialPackageIds.BpGameGuidanceFlowBo1FirstHalf =>
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
                    TutorialTargetNames.SurvivorPickPanel,
                    typeof(CharacterSelector).FullName!,
                    "角色选择器",
                    "这是角色选择器，不是普通下拉框。它支持角色名、拼音全拼和缩写搜索。输入后按空格可以搜索。按 Enter / Tab 或点击确认按钮完成选择。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSelectionConfirmed,
                    allowMissing: true)
            ],
            TutorialPackageIds.BpGlobalBanRecordBasic =>
            [
                Step(
                    TutorialTargetNames.GlobalBanRecordPanel,
                    "全局禁选记录",
                    "全局禁选会影响后续场次。新建下一局时，当局选择会被清空，但这些全局记录会被保留。",
                    ProductTourInteractionMode.BlockAll,
                    TutorialSignalIds.GlobalBanRecordUpdated,
                    allowMissing: true)
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
                    TutorialTargetNames.NewGameButton,
                    "新建对局",
                    "新建对局会清空当前局的选择结果，但会保留全局禁选。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.NewGameCreated),
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

        return builder.EndStep().Build().Steps[0];
    }

    private static ProductTourStep Step(
        string? targetName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .Step(targetName)
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
}
