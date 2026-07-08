using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
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
            TutorialPackageIds.FrontManageBpWindowLaunchBasic =>
            [
                ElementTagStep(
                    FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow),
                    "启动 BP 前台窗口",
                    "导播时，观众看到的是前台窗口。我们先只启动 BP 前台页面。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.BpWindowOpened)
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

    private static Task DelayForNavigationTransitionAsync(IServiceProvider _, CancellationToken cancellationToken) =>
        Task.Delay(450, cancellationToken);
}
