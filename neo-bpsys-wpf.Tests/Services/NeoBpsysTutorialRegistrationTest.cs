using neo_bpsys_wpf.ProductTour;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPFLocalizeExtension.Engine;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 测试内置 neo-bpsys-wpf 教程注册契约。
/// </summary>
public sealed class NeoBpsysTutorialRegistrationTest : IDisposable
{
    private static readonly ITutorialContentResolver ContentResolver = new NeoBpsysTutorialContentResolver();
    private readonly CultureInfo _previousCulture;

    public NeoBpsysTutorialRegistrationTest()
    {
        _previousCulture = LocalizeDictionary.Instance.Culture;
        TrySetCulture(CultureInfo.GetCultureInfo("zh-CN"));
    }

    public void Dispose()
    {
        TrySetCulture(_previousCulture);
    }

    private static void TrySetCulture(CultureInfo culture)
    {
        try
        {
            LocalizeDictionary.Instance.Culture = culture;
        }
        catch (Exception ex) when (IsClosedDispatcherLocalizationException(ex))
        {
        }
    }

    private static bool IsClosedDispatcherLocalizationException(Exception exception) =>
        exception is TaskCanceledException
        || (exception is AggregateException aggregate
            && aggregate.InnerExceptions.All(IsClosedDispatcherLocalizationException));

    [Fact]
    public void RegistrationKeepsBuiltInSequencesPackagesAndFirstRunIncludes()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();

        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        Assert.Equal(
            [
                TutorialPackageIds.MainNavigationFrontManage,
                TutorialPackageIds.MainNavigationTeamInfo,
                TutorialPackageIds.MainNavigationScore,
                TutorialPackageIds.MainNavigationSmartBp,
                TutorialPackageIds.MainNavigationDesignerV3,
                TutorialPackageIds.MainTeamSummaryBasic,
                TutorialPackageIds.MainNavigationBasic
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.Main));
        Assert.Equal(
            [
                TutorialPackageIds.TeamInfoTeamNameBasic,
                TutorialPackageIds.TeamInfoJsonImportPreset,
                TutorialPackageIds.TeamInfoPlayerManage
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.TeamInfo));
        Assert.Equal(
            [
                TutorialPackageIds.FrontManageOverview
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.FrontManage));
        Assert.Equal(
            [
                TutorialPackageIds.FrontManageWindowsBasic,
                TutorialPackageIds.FrontManageOpenDesigner,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic
            ],
            sequenceRegistry.GetSequence(FrontedWindowsView.TutorialPageKey));
        Assert.Equal(
            [
                TutorialPackageIds.FrontManageLayoutPackagesBasic
            ],
            sequenceRegistry.GetSequence(FrontedLayoutPackagesView.TutorialPageKey));
        Assert.Equal(
            [
                TutorialPackageIds.DesignerV3Overview,
                TutorialPackageIds.DesignerV3LayoutEditBasic,
                TutorialPackageIds.DesignerV3PackageImportExport,
                TutorialPackageIds.DesignerV3HelpBasic
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3));
        Assert.Equal(
            [
                TutorialPackageIds.DesignerV3BehaviorPanelOverview,
                TutorialPackageIds.DesignerV3BehaviorPanelTriggerBasic,
                TutorialPackageIds.DesignerV3BehaviorPanelActionBasic,
                TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3BehaviorPanel));
        Assert.Equal(
            [
                TutorialPackageIds.DesignerV3AnimationEditorOverview,
                TutorialPackageIds.DesignerV3AnimationEditorTimelineBasic,
                TutorialPackageIds.DesignerV3AnimationEditorKeyFrameBasic,
                TutorialPackageIds.DesignerV3AnimationEditorPreviewBasic,
                TutorialPackageIds.DesignerV3AnimationEditorHelpBasic
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3AnimationEditor));
        Assert.Empty(sequenceRegistry.GetSequence(TutorialPageKeys.SmartBp));
        Assert.Equal(47, packageRegistry.GetPackages().Count);

        var firstRun = flowRegistry.GetFlow(TutorialFlowIds.FirstRunStandardBp);
        Assert.NotNull(firstRun);
        Assert.Equal(1, firstRun.Version);
        Assert.Equal(
            [
                TutorialPackageIds.MainNavigationFrontManage,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialPackageIds.MainNavigationTeamInfo,
                TutorialPackageIds.TeamInfoTeamNameBasic,
                TutorialPackageIds.MainTeamSummaryBasic,
                TutorialPackageIds.TeamInfoJsonImportPreset,
                TutorialPackageIds.TeamInfoPlayerManage,
                TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
                TutorialPackageIds.BpGameGuidanceStartBasic,
                TutorialPackageIds.BpGameGuidanceCurrentStepBasic,
                TutorialPackageIds.MapBpBanMapOperationBasic,
                TutorialPackageIds.MapBpNextToPickMapBasic,
                TutorialPackageIds.MapBpPickMapOperationBasic,
                TutorialPackageIds.BpCharacterSelectorBasic,
                TutorialPackageIds.BpPickSelectFourSurvivorsBasic,
                TutorialPackageIds.BpGlobalBanRecordBasic,
                TutorialPackageIds.BpCharacterChangerBasic,
                TutorialPackageIds.BpTalentTraitBasic,
                TutorialPackageIds.BpGameGuidanceEndBasic,
                TutorialPackageIds.MainNavigationScore,
                TutorialPackageIds.ScoreBasic,
                TutorialPackageIds.GameManageNewGameBasic,
                TutorialPackageIds.NextGameBasic,
                TutorialPackageIds.GameManageGlobalBanCarryOver
            ],
            firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.SmartBpModuleShell, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.SmartBpModuleContentOverview, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.SmartBpCaptureBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.SmartBpRegionEditorBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.SmartBpFullBpFlowBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.SmartBpPostGameAutoFill, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3Overview, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3LayoutEditBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3PropertyPanelBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3BehaviorEditBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3PackageImportExport, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3HelpBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.FrontManageWindowsBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.FrontManageOpenDesigner, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.FrontManageLayoutPackagesBasic, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.MainNavigationSmartBp, firstRun.IncludedPackageIds);
        Assert.DoesNotContain(TutorialPackageIds.MainNavigationDesignerV3, firstRun.IncludedPackageIds);
        Assert.Equal(30, firstRun.Items.Count);
        Assert.Equal(
            TutorialPackageIds.MainNavigationFrontManage,
            Assert.IsType<PackageFlowItem>(firstRun.Items[1]).PackageId);
        Assert.Equal(
            [
                TutorialPackageIds.MainNavigationFrontManage,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialPackageIds.MainNavigationTeamInfo,
                TutorialPackageIds.TeamInfoTeamNameBasic,
                TutorialPackageIds.MainTeamSummaryBasic,
                TutorialPackageIds.TeamInfoJsonImportPreset,
                TutorialPackageIds.TeamInfoPlayerManage,
                TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
                TutorialPackageIds.BpGameGuidanceStartBasic,
                TutorialPackageIds.BpGameGuidanceCurrentStepBasic,
                TutorialPackageIds.MapBpBanMapOperationBasic,
                TutorialPackageIds.MapBpNextToPickMapBasic,
                TutorialPackageIds.MapBpPickMapOperationBasic,
                TutorialPackageIds.BpCharacterSelectorBasic,
                TutorialPackageIds.BpPickSelectFourSurvivorsBasic,
                TutorialPackageIds.BpGlobalBanRecordBasic,
                TutorialPackageIds.BpCharacterChangerBasic,
                TutorialPackageIds.BpTalentTraitBasic,
                TutorialPackageIds.BpGameGuidanceEndBasic,
                TutorialPackageIds.MainNavigationScore,
                TutorialPackageIds.ScoreBasic,
                TutorialPackageIds.GameManageNewGameBasic,
                TutorialPackageIds.NextGameBasic,
                TutorialPackageIds.GameManageGlobalBanCarryOver
            ],
            GetPackageFlowItemIds(firstRun));

        var navigationProbe = flowRegistry.GetFlow(TutorialFlowIds.Phase4ANavigationProbe);
        Assert.NotNull(navigationProbe);
        Assert.Equal(
            [TutorialPackageIds.MainNavigationTeamInfo],
            navigationProbe.IncludedPackageIds);
        Assert.Collection(
            navigationProbe.Items,
            item => Assert.IsType<DialogueFlowItem>(item),
            item => Assert.Equal(TutorialPackageIds.MainNavigationTeamInfo, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.IsType<DialogueFlowItem>(item));

        var realTargetProbe = flowRegistry.GetFlow(TutorialFlowIds.Phase4RealTargetProbe);
        Assert.NotNull(realTargetProbe);
        Assert.Equal(
            [
                TutorialPackageIds.MainNavigationFrontManage,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialPackageIds.MainNavigationTeamInfo,
                TutorialPackageIds.TeamInfoTeamNameBasic,
                TutorialPackageIds.MainTeamSummaryBasic,
                TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
                TutorialPackageIds.BpGameGuidanceStartBasic
            ],
            realTargetProbe.IncludedPackageIds);
        Assert.Collection(
            realTargetProbe.Items,
            item => Assert.IsType<DialogueFlowItem>(item),
            item => Assert.Equal(TutorialPackageIds.MainNavigationFrontManage, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.Equal(TutorialPackageIds.FrontManageBpWindowLaunchBasic, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.IsType<ActionFlowItem>(item),
            item => Assert.Equal(TutorialPackageIds.MainNavigationTeamInfo, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.Equal(TutorialPackageIds.TeamInfoTeamNameBasic, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.IsType<ActionFlowItem>(item),
            item => Assert.Equal(TutorialPackageIds.MainTeamSummaryBasic, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.Equal(TutorialPackageIds.GameManageGameProgressBo1FirstHalf, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.Equal(TutorialPackageIds.BpGameGuidanceStartBasic, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.IsType<DialogueFlowItem>(item));
        Assert.Equal(
            [
                TutorialPackageIds.MainNavigationFrontManage,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialPackageIds.MainNavigationTeamInfo,
                TutorialPackageIds.TeamInfoTeamNameBasic,
                TutorialPackageIds.MainTeamSummaryBasic,
                TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
                TutorialPackageIds.BpGameGuidanceStartBasic
            ],
            GetPackageFlowItemIds(realTargetProbe));

        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MainNavigationFrontManage);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MainNavigationTeamInfo);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MainNavigationScore);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MainTeamSummaryBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.TeamInfoTeamNameBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.TeamInfoJsonImportPreset);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.GameManageGameProgressBo1FirstHalf);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.GameManageNewGameBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpGameGuidanceStartBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpGameGuidanceCurrentStepBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MapBpBanMapOperationBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MapBpNextToPickMapBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpPickSelectFourSurvivorsBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpCharacterChangerBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpTalentTraitBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpGameGuidanceEndBasic);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.TeamInfoBasic);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.TeamInfoJsonImport);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.TeamInfoAdvanced);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpSharedBasic);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpCharacterSelectorAdvanced);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.GameManageImportExport);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.ScoreFrontedSync);
        Assert.DoesNotContain(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.ScoreAdvanced);

        var frontManagePackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.FrontManageBpWindowLaunchBasic);
        var frontManageStep = Assert.Single(frontManagePackage.Steps);
        Assert.Equal(TutorialTargetKind.ElementTag, frontManageStep.TargetKind);
        Assert.Equal(
            FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow),
            frontManageStep.TargetKey);
        Assert.Null(frontManageStep.TargetName);

        var navigationPackages = new[]
        {
            TutorialPackageIds.MainNavigationFrontManage,
            TutorialPackageIds.MainNavigationTeamInfo,
            TutorialPackageIds.MainNavigationScore
        };
        foreach (var packageId in navigationPackages)
        {
            var navigationPackage = Assert.Single(
                packageRegistry.GetPackages(),
                package => package.PackageId == packageId);
            var navigationStep = Assert.Single(navigationPackage.Steps);
            Assert.Equal(TutorialTargetKind.NavigationItem, navigationStep.TargetKind);
            Assert.Contains(navigationStep.PostStepActions, action => action.Name == "CompleteNavigationStep");
        }

        var globalBanPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.BpGlobalBanRecordBasic);
        var globalBanStep = Assert.Single(globalBanPackage.Steps);
        Assert.Equal(TutorialTargetKind.Name, globalBanStep.TargetKind);
        Assert.Equal(TutorialTargetNames.GlobalBanRecordPanel, globalBanStep.TargetName);
        Assert.Equal(ProductTourInteractionMode.AllowTargetOnly, globalBanStep.InteractionMode);
        Assert.Null(globalBanStep.WaitForSignalId);

        var playerManagePackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.TeamInfoPlayerManage);
        var playerManageStep = Assert.Single(playerManagePackage.Steps);
        Assert.Equal(TutorialTargetNames.HomePlayerPositionPanel, playerManageStep.TargetName);
        Assert.Equal(ProductTourInteractionMode.AllowTargetOnly, playerManageStep.InteractionMode);

        var teamNamePackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.TeamInfoTeamNameBasic);
        Assert.Equal(
            [
                TutorialTargetNames.HomeTeamNameInput,
                TutorialTargetNames.HomeTeamNameConfirmButton,
                TutorialTargetNames.HomeTeamLogoButton
            ],
            teamNamePackage.Steps.Select(step => step.TargetName).ToArray());

        var jsonPresetPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.TeamInfoJsonImportPreset);
        Assert.Equal(
            [
                TutorialTargetNames.HomeTeamJsonImportButton,
                TutorialTargetNames.HomePlayerListPanel,
                TutorialTargetNames.AwayTeamJsonImportButton,
                TutorialTargetNames.AwayPlayerListPanel
            ],
            jsonPresetPackage.Steps.Select(step => step.TargetName).ToArray());
        Assert.Equal(
            ["SmoothScrollTo(AwayTeamInfoCard)", "Delay(250ms)", "SetAwayTeamJsonPickerHint"],
            jsonPresetPackage.Steps[2].PreStepActions.Select(action => action.Name).ToArray());
        Assert.All(
            jsonPresetPackage.Steps.Where(step => step.TargetName is TutorialTargetNames.HomePlayerListPanel or TutorialTargetNames.AwayPlayerListPanel),
            step =>
            {
                Assert.Equal(ProductTourInteractionMode.AllowTargetOnly, step.InteractionMode);
                Assert.Equal(TutorialAvatarPose.LeftTop, step.AvatarPose);
            });

        var banMapPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.MapBpBanMapOperationBasic);
        var banMapStep = Assert.Single(banMapPackage.Steps);
        Assert.Equal(TutorialTargetNames.MapBanOperationBorder, banMapStep.TargetName);
        Assert.Contains("Ban 地图", ContentResolver.Resolve(banMapStep.DescriptionKey), StringComparison.Ordinal);

        var nextMapPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.MapBpNextToPickMapBasic);
        var nextMapStep = Assert.Single(nextMapPackage.Steps);
        Assert.Equal(TutorialTargetNames.NextGuidanceStepButton, nextMapStep.TargetName);
        Assert.Contains("进入选择地图", ContentResolver.Resolve(nextMapStep.DescriptionKey), StringComparison.Ordinal);
        Assert.DoesNotContain("进入角色 BP", ContentResolver.Resolve(nextMapStep.DescriptionKey), StringComparison.Ordinal);

        var pickFourPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.BpPickSelectFourSurvivorsBasic);
        var pickFourStep = Assert.Single(pickFourPackage.Steps);
        Assert.Equal(TutorialTargetKind.Name, pickFourStep.TargetKind);
        Assert.Equal(TutorialTargetNames.SurvivorPickSelectorGroupBorder, pickFourStep.TargetName);
        Assert.Equal(TutorialSignalIds.PickSurvivorSlotsCompleted, pickFourStep.WaitForSignalId);

        Assert.Equal(
            firstRun.IncludedPackageIds,
            GetPackageFlowItemIds(firstRun));

        var talentTraitPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.BpTalentTraitBasic);
        var talentTraitStep = talentTraitPackage.Steps[0];
        Assert.Equal(TutorialTargetNames.TalentTraitSelectorPanel, talentTraitStep.TargetName);
    }

    [Fact]
    public async Task HostRegistrationDoesNotOwnModuleTutorialPackages()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var packages = CreateRegisteredPackages();
            Assert.DoesNotContain(packages, package => package.PageKey == TutorialPageKeys.SmartBp);

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// 验证首次运行的对局进度步骤会在要求用户选择 BO1 上半场之前重置对局。
    /// </summary>
    [Fact]
    public async Task MainWindowGameProgressPreStepActionResetsProgressAndStopsGuidance()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame(GameProgress.Game1FirstHalf);
            var guidance = new Mock<IGameGuidanceService>();
            guidance.SetupGet(service => service.IsGuidanceStarted).Returns(true);
            var action = GetSinglePreStepAction(TutorialPackageIds.GameManageGameProgressBo1FirstHalf);

            await action.ExecuteAsync(
                CreateActionContext(game, guidance.Object, TutorialPackageIds.GameManageGameProgressBo1FirstHalf),
                CancellationToken.None);

            Assert.Equal(GameProgress.Free, game.GameProgress);
            guidance.Verify(service => service.StopGuidance(), Times.Once);
        });
    }

    /// <summary>
    /// 验证 BP 引导开始步骤会在启动引导之前准备 BO1 上半场。
    /// </summary>
    [Fact]
    public async Task MainWindowBpGuidancePreStepActionSelectsFirstHalfAndStopsGuidance()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame(GameProgress.Free);
            var guidance = new Mock<IGameGuidanceService>();
            guidance.SetupGet(service => service.IsGuidanceStarted).Returns(true);
            var action = GetSinglePreStepAction(TutorialPackageIds.BpGameGuidanceStartBasic);

            await action.ExecuteAsync(
                CreateActionContext(game, guidance.Object, TutorialPackageIds.BpGameGuidanceStartBasic),
                CancellationToken.None);

            Assert.Equal(GameProgress.Game1FirstHalf, game.GameProgress);
            guidance.Verify(service => service.StopGuidance(), Times.Once);
        });
    }

    /// <summary>
    /// 验证引导 post-step 动作会从教程动作上下文中解析引导服务。
    /// </summary>
    /// <param name="packageId">包含 post-step 动作的教程包 id。</param>
    /// <param name="stepIndex">期望的引导步骤索引。</param>
    [Theory]
    [InlineData(TutorialPackageIds.MapBpPickMapOperationBasic, 3)]
    [InlineData(TutorialPackageIds.BpCharacterSelectorBasic, 4)]
    [InlineData(TutorialPackageIds.BpGlobalBanRecordBasic, 9)]
    [InlineData(TutorialPackageIds.BpCharacterChangerBasic, 10)]
    public async Task GuidancePostStepActionsUseContextServices(string packageId, int stepIndex)
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame(GameProgress.Game1FirstHalf);
            var guidance = new Mock<IGameGuidanceService>();
            guidance
                .Setup(service => service.MoveToStepAsync(stepIndex, true))
                .ReturnsAsync((string?)null);
            var action = GetSinglePostStepAction(packageId);

            await action.ExecuteAsync(
                CreateActionContext(game, guidance.Object, packageId),
                CancellationToken.None);

            guidance.Verify(service => service.MoveToStepAsync(stepIndex, true), Times.Once);
        });
    }

    /// <summary>
    /// 验证教程定义不会通过全局应用宿主绕过动作上下文。
    /// </summary>
    [Fact]
    public void TutorialDefinitionFiles_ShouldNotUseIAppHost()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(
            Path.Combine(root, "neo-bpsys-wpf"),
            "*.Tutorials.cs",
            SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("IAppHost.Host", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TeamInfo_ShouldNotRegisterDuplicateTeamNamePackages()
    {
        var packages = CreateRegisteredPackages();
        Assert.Single(packages, package => package.PackageId == TutorialPackageIds.TeamInfoTeamNameBasic);
        Assert.DoesNotContain(packages, package => package.PackageId == TutorialPackageIds.TeamInfoBasic);
        Assert.DoesNotContain(packages, package => package.PackageId == TutorialPackageIds.TeamInfoJsonImport);
        Assert.DoesNotContain(packages, package => package.PackageId == TutorialPackageIds.TeamInfoAdvanced);
    }

    [Fact]
    public void TutorialSequences_ShouldNotContainFallbackPackages()
    {
        var sequenceRegistry = CreateRegisteredSequences();
        var forbiddenPackageIds = new[]
        {
            TutorialPackageIds.BpSharedBasic,
            TutorialPackageIds.BpCharacterSelectorAdvanced,
            TutorialPackageIds.TeamInfoAdvanced,
            TutorialPackageIds.GameManageImportExport,
            TutorialPackageIds.ScoreFrontedSync,
            TutorialPackageIds.ScoreAdvanced
        };

        var builtInSequenceKeys = new[]
        {
            TutorialPageKeys.Main,
            TutorialPageKeys.TeamInfo,
            TutorialPageKeys.FrontManage,
            FrontedWindowsView.TutorialPageKey,
            FrontedLayoutPackagesView.TutorialPageKey,
            TutorialPageKeys.DesignerV3,
            TutorialPageKeys.DesignerV3BehaviorPanel,
            TutorialPageKeys.DesignerV3AnimationEditor,
            TutorialPageKeys.SmartBp,
            TutorialPageKeys.GameManage,
            TutorialPageKeys.BpGameGuidance,
            BanSurPage.TutorialPageKey,
            PickPage.TutorialPageKey,
            ScorePage.TutorialPageKey
        };

        Assert.DoesNotContain(
            builtInSequenceKeys.SelectMany(sequenceRegistry.GetSequence),
            forbiddenPackageIds.Contains);
    }

    [Fact]
    public void FirstRun_FlowItems_ShouldMatchExpectedOrderExactly()
    {
        var firstRun = CreateRegisteredFlow(TutorialFlowIds.FirstRunStandardBp);

        Assert.Equal(
            [
                TutorialPackageIds.MainNavigationFrontManage,
                TutorialPackageIds.FrontManageBpWindowLaunchBasic,
                TutorialPackageIds.MainNavigationTeamInfo,
                TutorialPackageIds.TeamInfoTeamNameBasic,
                TutorialPackageIds.MainTeamSummaryBasic,
                TutorialPackageIds.TeamInfoJsonImportPreset,
                TutorialPackageIds.TeamInfoPlayerManage,
                TutorialPackageIds.GameManageGameProgressBo1FirstHalf,
                TutorialPackageIds.BpGameGuidanceStartBasic,
                TutorialPackageIds.BpGameGuidanceCurrentStepBasic,
                TutorialPackageIds.MapBpBanMapOperationBasic,
                TutorialPackageIds.MapBpNextToPickMapBasic,
                TutorialPackageIds.MapBpPickMapOperationBasic,
                TutorialPackageIds.BpCharacterSelectorBasic,
                TutorialPackageIds.BpPickSelectFourSurvivorsBasic,
                TutorialPackageIds.BpGlobalBanRecordBasic,
                TutorialPackageIds.BpCharacterChangerBasic,
                TutorialPackageIds.BpTalentTraitBasic,
                TutorialPackageIds.BpGameGuidanceEndBasic,
                TutorialPackageIds.MainNavigationScore,
                TutorialPackageIds.ScoreBasic,
                TutorialPackageIds.GameManageNewGameBasic,
                TutorialPackageIds.NextGameBasic,
                TutorialPackageIds.GameManageGlobalBanCarryOver
            ],
            GetPackageFlowItemIds(firstRun));
    }

    [Fact]
    public void FirstRun_IncludedPackages_ShouldMatchActualPackageFlowItems()
    {
        var firstRun = CreateRegisteredFlow(TutorialFlowIds.FirstRunStandardBp);

        Assert.Equal(firstRun.IncludedPackageIds, GetPackageFlowItemIds(firstRun));
    }

    [Fact]
    public void PickFourSurvivorsTutorial_ShouldUseGroupTargetNotFirstSelector()
    {
        var package = Assert.Single(
            CreateRegisteredPackages(),
            package => package.PackageId == TutorialPackageIds.BpPickSelectFourSurvivorsBasic);
        var step = Assert.Single(package.Steps);

        Assert.Equal(TutorialTargetKind.Name, step.TargetKind);
        Assert.Equal(TutorialTargetNames.SurvivorPickSelectorGroupBorder, step.TargetName);
        Assert.NotEqual(TutorialTargetNames.FirstSurvivorPickSelectorHost, step.TargetName);
        Assert.Equal(TutorialSignalIds.PickSurvivorSlotsCompleted, step.WaitForSignalId);
    }

    [Fact]
    public void SmartBpTutorialTargets_ShouldResolveToCorrectControls()
    {
        var xaml = File.ReadAllText(GetRepositoryPath("neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));

        Assert.Contains("x:Name=\"SmartBpWindowSelector\"", xaml);
        Assert.Contains("x:Name=\"SmartBpStartCaptureButton\"", ExtractElementByCommand(xaml, "StartCaptureCommand"));
        Assert.Contains("x:Name=\"SmartBpPreviewButton\"", ExtractElementByCommand(xaml, "OpenPreviewWindowCommand"));
        Assert.Contains("x:Name=\"SmartBpStopCaptureButton\"", ExtractElementByCommand(xaml, "StopCaptureCommand"));
        Assert.DoesNotContain("x:Name=\"SmartBpStartCaptureButton\"", ExtractElementByCommand(xaml, "RefreshActiveWindowsCommand"));
        Assert.DoesNotContain("x:Name=\"SmartBpStopCaptureButton\"", ExtractElementByCommand(xaml, "OpenWindowPickerCommand"));
    }

    [Fact]
    public void DesignerLayoutEditPreviewStepTargetsDesignerSurfaceFrame()
    {
        var package = Assert.Single(
            CreateRegisteredPackages(),
            package => package.PackageId == TutorialPackageIds.DesignerV3LayoutEditBasic);
        var previewStep = Assert.Single(
            package.Steps,
            step => step.TitleKey == "Step.DesignerV3LayoutEditBasic.2.Title"
                && ContentResolver.Resolve(step.DescriptionKey).Contains("点击画布上的一个控件", StringComparison.Ordinal));

        Assert.Equal("PreviewWorkspace", previewStep.TargetName);
        Assert.NotEqual(TutorialTargetNames.PreviewCanvas, previewStep.TargetName);
    }

    [Fact]
    public void DesignerTutorialStartsWithWelcomeAndEndsWithHelpButton()
    {
        var packages = CreateRegisteredPackages();
        var overview = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.DesignerV3Overview);
        var help = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.DesignerV3HelpBasic);

        var dialogue = Assert.IsType<TutorialPackageDialogueItem>(overview.Items[0]).Dialogue;
        Assert.Contains(ContentResolver.ResolveLines(dialogue.LinesKey), line => line.Contains("欢迎来到 v3 设计器", StringComparison.Ordinal));
        Assert.Contains(ContentResolver.ResolveLines(dialogue.LinesKey), line => line.Contains("详细修改前台界面", StringComparison.Ordinal));
        Assert.DoesNotContain(overview.Steps, step => step.TargetName == TutorialTargetNames.BehaviorPanelHost);

        var layoutSteps = packages.Single(package => package.PackageId == TutorialPackageIds.DesignerV3LayoutEditBasic).Steps;
        var previewStep = Assert.Single(
            layoutSteps,
            step => step.TargetName == "PreviewWorkspace"
                && ContentResolver.Resolve(step.DescriptionKey).Contains("点击画布上的一个控件", StringComparison.Ordinal));
        Assert.Contains("点击画布上的一个控件", ContentResolver.Resolve(previewStep.DescriptionKey), StringComparison.Ordinal);
        Assert.DoesNotContain(layoutSteps, step => step.TargetName == TutorialTargetNames.InteractionLayer);
        Assert.DoesNotContain(layoutSteps.Select(step => step.Title), title => title.Contains("交互层", StringComparison.Ordinal));

        var propertySteps = packages.Single(package => package.PackageId == TutorialPackageIds.DesignerV3PropertyPanelBasic).Steps;
        Assert.Contains(propertySteps, step => step.TitleKey == "Step.DesignerV3PropertyPanelBasic.3.Title");

        var finalStep = Assert.Single(help.Steps);
        Assert.Equal(TutorialTargetNames.DesignerHelpButton, finalStep.TargetName);
        Assert.Contains("v3 编辑器的详细说明", ContentResolver.Resolve(finalStep.DescriptionKey), StringComparison.Ordinal);
        Assert.False(finalStep.AllowMissingTarget);
        Assert.Contains(finalStep.PreStepActions, action => action.Name == "ScrollDesignerHelpButtonIntoView");
    }

    [Fact]
    public void DesignerTutorial_ShouldNotContainImplementationTerms()
    {
        var packages = CreateRegisteredPackages()
            .Where(package => package.PageKey is TutorialPageKeys.DesignerV3
                or TutorialPageKeys.DesignerV3BehaviorPanel
                or TutorialPageKeys.DesignerV3AnimationEditor)
            .ToArray();
        var forbiddenTerms = new[]
        {
            "交互层",
            "InteractionLayer",
            "ZoomHost",
            "PreviewZoomHost",
            "DropZone",
            "LayerTopDropZone",
            "LayerBottomDropZone",
            "VisualTree",
            "DataContext",
            "Dispatcher",
            "FrameworkElement"
        };

        foreach (var step in packages.SelectMany(package => package.Steps))
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, step.Title, StringComparison.Ordinal);
                Assert.DoesNotContain(term, step.Description, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void DesignerBaseSequence_ShouldNotStopBeforeHelpWhenOptionalTargetsMissing()
    {
        var packages = CreateRegisteredPackages();
        var importExport = Assert.Single(
            packages,
            package => package.PackageId == TutorialPackageIds.DesignerV3PackageImportExport);
        var help = Assert.Single(
            packages,
            package => package.PackageId == TutorialPackageIds.DesignerV3HelpBasic);

        Assert.Contains(importExport.Steps, step => step.TargetName is null && step.TargetKind == TutorialTargetKind.None);
        Assert.Contains(importExport.Steps, step => step.TitleKey == "Step.DesignerV3PackageImportExport.0.Title");

        var helpStep = Assert.Single(help.Steps);
        Assert.Equal(TutorialTargetNames.DesignerHelpButton, helpStep.TargetName);
        Assert.False(helpStep.AllowMissingTarget);
    }

    [Fact]
    public void FrontManagePage_ShouldNotDiscoverChildTutorialOwners()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml.cs"));

        Assert.DoesNotContain("TryFindVisibleDescendant", source);
        Assert.DoesNotContain("TryResolveCurrentChildTutorial", source);
        Assert.DoesNotContain("RunCurrentChildTutorial", source);
    }

    [Fact]
    public void FrontManage_Loaded_ShouldNotImmediatelyScheduleChildTutorial()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml.cs"));
        var loadedBlockStart = source.IndexOf("Loaded += async (_, _) =>", StringComparison.Ordinal);
        Assert.True(loadedBlockStart >= 0);
        var visibleChangedStart = source.IndexOf("IsVisibleChanged", loadedBlockStart, StringComparison.Ordinal);
        Assert.True(visibleChangedStart > loadedBlockStart);
        var loadedBlock = source[loadedBlockStart..visibleChangedStart];

        Assert.Contains("TryRunTutorialAsync", loadedBlock);
        Assert.DoesNotContain("ScheduleCurrentChildTutorial();", loadedBlock);
        Assert.Contains("RunSequenceAsync(this, TutorialPageKeys.FrontManage, _tutorialLifetime.Token)", source);
    }

    [Fact]
    public void LayoutPackagesView_ShouldNotDependOnParentVisualTreeScan()
    {
        var parentSource = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml.cs"));
        var childSource = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManage",
            "FrontedLayoutPackagesView.xaml.cs"));

        Assert.DoesNotContain("FrontManageTabs.Navigated", parentSource);
        Assert.DoesNotContain("FrontManageTabs.SelectionChanged", parentSource);
        Assert.Contains("Loaded +=", childSource);
        Assert.Contains("IsVisibleChanged +=", childSource);
        Assert.Contains("RunSequenceAsync(this, TutorialPageKey", childSource);
    }

    [Fact]
    public void BehaviorPanelAndAnimationEditorSequencesEndWithHelp()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();

        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        Assert.Equal(
            TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic,
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3BehaviorPanel)[^1]);
        Assert.Equal(
            TutorialPackageIds.DesignerV3AnimationEditorHelpBasic,
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3AnimationEditor)[^1]);

        var behaviorHelp = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic);
        Assert.Equal(TutorialTargetNames.BehaviorHelpButton, Assert.Single(behaviorHelp.Steps).TargetName);

        var animationHelp = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.DesignerV3AnimationEditorHelpBasic);
        Assert.Equal(TutorialTargetNames.AnimationEditorHelpButton, Assert.Single(animationHelp.Steps).TargetName);
    }

    [Fact]
    public void FrontManageLayoutPackageTutorialExplainsBuiltInLayoutCopyBehavior()
    {
        var package = Assert.Single(
            CreateRegisteredPackages(),
            package => package.PackageId == TutorialPackageIds.FrontManageLayoutPackagesBasic);
        var text = string.Join("\n", package.Steps.Select(step => ContentResolver.Resolve(step.DescriptionKey)));

        Assert.Contains("内置布局无法被直接修改", text, StringComparison.Ordinal);
        Assert.Contains("自动切换到一个新的用户自定义布局", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeamInfoJsonImportPresetUsesExamplesDirectoryForCommonJsonPicker()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var package = Assert.Single(
                CreateRegisteredPackages(),
                package => package.PackageId == TutorialPackageIds.TeamInfoJsonImportPreset);
            Assert.Equal(4, package.Steps.Count);

            var expectedDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples");

            var homeImportStep = package.Steps[0];
            var homeImportAction = Assert.Single(homeImportStep.PreStepActions);
            await homeImportAction.ExecuteAsync(
                new TutorialStepActionContext
                {
                    Services = new EmptyServiceProvider(),
                    Owner = new Grid(),
                    Step = homeImportStep
                },
                CancellationToken.None);
            var homeHint = TutorialFilePickerHints.ConsumeNextJsonPickerHint();
            Assert.Equal(expectedDirectory, homeHint.InitialDirectory);
            Assert.Contains("队伍信息导入示例-Wolves.json", homeHint.Title);

            var awayImportStep = package.Steps[2];
            var awayImportAction = Assert.Single(
                awayImportStep.PreStepActions,
                action => action.Name == "SetAwayTeamJsonPickerHint");
            await awayImportAction.ExecuteAsync(
                new TutorialStepActionContext
                {
                    Services = new EmptyServiceProvider(),
                    Owner = new Grid(),
                    Step = awayImportStep
                },
                CancellationToken.None);
            var awayHint = TutorialFilePickerHints.ConsumeNextJsonPickerHint();
            Assert.Equal(expectedDirectory, awayHint.InitialDirectory);
            Assert.Contains("队伍信息导入示例-GR.json", awayHint.Title);

            Assert.Null(TutorialFilePickerHints.ConsumeNextJsonPickerHint().InitialDirectory);
        });
    }

    private static string[] GetPackageFlowItemIds(TutorialFlowDefinition flow) =>
        flow.Items.OfType<PackageFlowItem>().Select(item => item.PackageId).ToArray();

    private static IReadOnlyCollection<TutorialPackageDefinition> CreateRegisteredPackages()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();

        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        return packageRegistry.GetPackages();
    }

    private static TutorialStepAction GetSinglePreStepAction(string packageId)
    {
        var package = Assert.Single(CreateRegisteredPackages(), package => package.PackageId == packageId);
        var step = Assert.Single(package.Steps);
        return Assert.Single(step.PreStepActions);
    }

    private static TutorialStepAction GetSinglePostStepAction(string packageId)
    {
        var package = Assert.Single(CreateRegisteredPackages(), package => package.PackageId == packageId);
        var step = Assert.Single(package.Steps, step => step.PostStepActions.Count > 0);
        return Assert.Single(step.PostStepActions);
    }

    private static TutorialStepActionContext CreateActionContext(
        Game game,
        IGameGuidanceService guidanceService,
        string packageId)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(service => service.CurrentGame).Returns(game);
        var services = new ServiceCollection()
            .AddSingleton(shared.Object)
            .AddSingleton(guidanceService)
            .BuildServiceProvider();
        var package = Assert.Single(CreateRegisteredPackages(), package => package.PackageId == packageId);
        var step = Assert.Single(
            package.Steps,
            step => step.PreStepActions.Count > 0 || step.PostStepActions.Count > 0);

        return new TutorialStepActionContext
        {
            Services = services,
            Owner = new FrameworkElement(),
            Step = step
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "neo-bpsys-wpf.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static Game CreateGame(GameProgress progress) =>
        new(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            progress);

    private static TutorialSequenceRegistry CreateRegisteredSequences()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();

        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        return sequenceRegistry;
    }

    private static TutorialFlowDefinition CreateRegisteredFlow(string flowId)
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();

        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        var flow = flowRegistry.GetFlow(flowId);
        Assert.NotNull(flow);
        return flow;
    }

    private static string ExtractElementByCommand(string xaml, string commandName)
    {
        var commandIndex = xaml.IndexOf($"Command=\"{{Binding {commandName}}}\"", StringComparison.Ordinal);
        Assert.True(commandIndex >= 0, $"Command not found: {commandName}");

        var start = xaml.LastIndexOf("<ui:Button", commandIndex, StringComparison.Ordinal);
        var end = xaml.IndexOf("/>", commandIndex, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Button element not found for command: {commandName}");
        return xaml[start..(end + 2)];
    }

    private static string GetRepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        object? IServiceProvider.GetService(Type serviceType) => null;
    }

}
