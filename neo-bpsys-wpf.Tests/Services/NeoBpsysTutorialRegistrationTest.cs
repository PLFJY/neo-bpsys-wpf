using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests built-in neo-bpsys-wpf tutorial registration contracts.
/// </summary>
public sealed class NeoBpsysTutorialRegistrationTest
{
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
                TutorialPackageIds.DesignerV3PropertyPanelBasic,
                TutorialPackageIds.DesignerV3BehaviorEditBasic,
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
        Assert.Equal(
            [
                TutorialPackageIds.SmartBpModuleContentOverview,
                TutorialPackageIds.SmartBpOcrModelDownloadBasic,
                TutorialPackageIds.SmartBpCaptureBasic,
                TutorialPackageIds.SmartBpRegionEditorBasic,
                TutorialPackageIds.SmartBpFullBpFlowBasic,
                TutorialPackageIds.SmartBpPostGameAutoFill
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.SmartBp));
        Assert.Equal(54, packageRegistry.GetPackages().Count);

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
        Assert.Contains("Ban 地图", banMapStep.Description, StringComparison.Ordinal);

        var nextMapPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.MapBpNextToPickMapBasic);
        var nextMapStep = Assert.Single(nextMapPackage.Steps);
        Assert.Equal(TutorialTargetNames.NextGuidanceStepButton, nextMapStep.TargetName);
        Assert.Contains("进入选择地图", nextMapStep.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("进入角色 BP", nextMapStep.Description, StringComparison.Ordinal);

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
    public async Task SmartBpPackagesDoNotUseCanRun()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var packages = CreateRegisteredPackages();
            var overview = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpModuleContentOverview);
            var capture = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpCaptureBasic);
            var region = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpRegionEditorBasic);
            var fullBpFlow = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpFullBpFlowBasic);
            var postGamePackage = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpPostGameAutoFill);

            Assert.Null(overview.CanRunWithOwner);
            Assert.Null(overview.CanRun);
            Assert.Null(capture.CanRunWithOwner);
            Assert.Null(capture.CanRun);
            Assert.Null(region.CanRunWithOwner);
            Assert.Null(region.CanRun);
            Assert.Null(fullBpFlow.CanRunWithOwner);
            Assert.Null(fullBpFlow.CanRun);
            Assert.Null(postGamePackage.CanRunWithOwner);
            Assert.Null(postGamePackage.CanRun);

            return Task.CompletedTask;
        });
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
        var previewStep = Assert.Single(package.Steps, step => step.Title == "预览画布");

        Assert.Equal("PreviewWorkspace", previewStep.TargetName);
        Assert.NotEqual(TutorialTargetNames.PreviewCanvas, previewStep.TargetName);
    }

    [Fact]
    public void DesignerTutorialStartsWithWelcomeAndEndsWithHelpButton()
    {
        var packages = CreateRegisteredPackages();
        var overview = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.DesignerV3Overview);
        var help = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.DesignerV3HelpBasic);

        Assert.Null(overview.Steps[0].TargetName);
        Assert.Contains("欢迎来到 v3 设计器", overview.Steps[0].Title, StringComparison.Ordinal);
        Assert.Contains("详细修改前台界面", overview.Steps[0].Description, StringComparison.Ordinal);
        Assert.DoesNotContain(overview.Steps, step => step.TargetName == TutorialTargetNames.BehaviorPanelHost);

        var layoutSteps = packages.Single(package => package.PackageId == TutorialPackageIds.DesignerV3LayoutEditBasic).Steps;
        var previewStep = Assert.Single(layoutSteps, step => step.TargetName == "PreviewWorkspace");
        Assert.Contains("点击画布上的一个控件", previewStep.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(layoutSteps, step => step.TargetName == TutorialTargetNames.InteractionLayer);
        Assert.DoesNotContain(layoutSteps.Select(step => step.Title), title => title.Contains("交互层", StringComparison.Ordinal));

        var propertySteps = packages.Single(package => package.PackageId == TutorialPackageIds.DesignerV3PropertyPanelBasic).Steps;
        Assert.Contains(propertySteps, step => step.Title == "行为和动画入口");

        var finalStep = Assert.Single(help.Steps);
        Assert.Equal(TutorialTargetNames.DesignerHelpButton, finalStep.TargetName);
        Assert.Contains("v3 编辑器的详细说明", finalStep.Description, StringComparison.Ordinal);
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
        Assert.Contains(importExport.Steps, step => step.Title == "保存、导入和导出");

        var helpStep = Assert.Single(help.Steps);
        Assert.Equal(TutorialTargetNames.DesignerHelpButton, helpStep.TargetName);
        Assert.False(helpStep.AllowMissingTarget);
    }

    [Fact]
    public void FrontManageChildTutorialHelperResolvesVisibleChildOwnerAndPageKey()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml.cs"));

        Assert.Contains("TryFindVisibleDescendant<FrontedWindowsView>", source);
        Assert.Contains("pageKey = FrontedWindowsView.TutorialPageKey;", source);
        Assert.Contains("TryFindVisibleDescendant<FrontedLayoutPackagesView>", source);
        Assert.Contains("pageKey = FrontedLayoutPackagesView.TutorialPageKey;", source);
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
    public void FrontManage_TabChanged_ShouldTriggerChildTutorial()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml.cs"));

        Assert.Contains("FrontManageTabs.Navigated += (_, _) => ScheduleCurrentChildTutorial();", source);
        Assert.Contains("FrontManageTabs.SelectionChanged += (_, _) => ScheduleCurrentChildTutorial();", source);
        Assert.Contains("FrontManageTabs.Navigate(typeof(FrontedLayoutPackagesView));", source);
        Assert.Contains("ScheduleCurrentChildTutorial();", source);
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
        var text = string.Join("\n", package.Steps.Select(step => step.Description));

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
