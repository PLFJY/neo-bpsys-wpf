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
                TutorialPackageIds.TeamInfoPlayerManage,
                TutorialPackageIds.TeamInfoBasic,
                TutorialPackageIds.TeamInfoJsonImport,
                TutorialPackageIds.TeamInfoAdvanced
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
                TutorialPackageIds.DesignerV3PackageImportExport,
                TutorialPackageIds.DesignerV3HelpBasic
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3));
        Assert.Equal(
            TutorialAutoRunStrategy.DrainSequence,
            sequenceRegistry.GetSequenceDefinition(TutorialPageKeys.DesignerV3).AutoRunStrategy);
        Assert.Equal(
            [
                TutorialPackageIds.DesignerV3BehaviorPanelOverview,
                TutorialPackageIds.DesignerV3BehaviorPanelTriggerBasic,
                TutorialPackageIds.DesignerV3BehaviorPanelActionBasic,
                TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3BehaviorPanel));
        Assert.Equal(
            TutorialAutoRunStrategy.DrainSequence,
            sequenceRegistry.GetSequenceDefinition(TutorialPageKeys.DesignerV3BehaviorPanel).AutoRunStrategy);
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
            TutorialAutoRunStrategy.DrainSequence,
            sequenceRegistry.GetSequenceDefinition(TutorialPageKeys.DesignerV3AnimationEditor).AutoRunStrategy);
        Assert.Equal(
            [
                TutorialPackageIds.SmartBpModuleShell,
                TutorialPackageIds.SmartBpModuleContentOverview,
                TutorialPackageIds.SmartBpCaptureBasic,
                TutorialPackageIds.SmartBpRegionEditorBasic,
                TutorialPackageIds.SmartBpFullBpFlowBasic,
                TutorialPackageIds.SmartBpPostGameAutoFill
            ],
            sequenceRegistry.GetSequence(TutorialPageKeys.SmartBp));
        Assert.Equal(
            TutorialAutoRunStrategy.SinglePendingPackage,
            sequenceRegistry.GetSequenceDefinition(TutorialPageKeys.FrontManage).AutoRunStrategy);
        Assert.Equal(
            TutorialAutoRunStrategy.SinglePendingPackage,
            sequenceRegistry.GetSequenceDefinition(FrontedWindowsView.TutorialPageKey).AutoRunStrategy);
        Assert.Equal(
            TutorialAutoRunStrategy.SinglePendingPackage,
            sequenceRegistry.GetSequenceDefinition(FrontedLayoutPackagesView.TutorialPageKey).AutoRunStrategy);
        Assert.Equal(
            TutorialAutoRunStrategy.SinglePendingPackage,
            sequenceRegistry.GetSequenceDefinition(TutorialPageKeys.SmartBp).AutoRunStrategy);
        Assert.Equal(55, packageRegistry.GetPackages().Count);

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
                TutorialPackageIds.MapBpCompletionNextBasic,
                TutorialPackageIds.BpCharacterSelectorBasic,
                TutorialPackageIds.BpPickCharacterBasic,
                TutorialPackageIds.BpGlobalBanRecordBasic,
                TutorialPackageIds.MainNavigationScore,
                TutorialPackageIds.ScoreBasic,
                TutorialPackageIds.GameManageNewGameBasic,
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
        Assert.Equal(23, firstRun.Items.Count);
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
                TutorialPackageIds.MapBpCompletionNextBasic,
                TutorialPackageIds.BpCharacterSelectorBasic,
                TutorialPackageIds.BpPickCharacterBasic,
                TutorialPackageIds.BpGlobalBanRecordBasic,
                TutorialPackageIds.MainNavigationScore,
                TutorialPackageIds.ScoreBasic,
                TutorialPackageIds.GameManageNewGameBasic,
                TutorialPackageIds.GameManageGlobalBanCarryOver
            ],
            GetPackageFlowItemIds(firstRun));

        var navigationProbe = flowRegistry.GetFlow(TutorialFlowIds.Phase4ANavigationProbe);
        Assert.NotNull(navigationProbe);
        Assert.Empty(navigationProbe.IncludedPackageIds);
        Assert.Collection(
            navigationProbe.Items,
            item => Assert.IsType<DialogueFlowItem>(item),
            item => Assert.Equal(TutorialPackageIds.MainNavigationTeamInfo, Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.IsType<DialogueFlowItem>(item));

        var realTargetProbe = flowRegistry.GetFlow(TutorialFlowIds.Phase4RealTargetProbe);
        Assert.NotNull(realTargetProbe);
        Assert.Empty(realTargetProbe.IncludedPackageIds);
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
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.MapBpCompletionNextBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.BpPickCharacterBasic);
        Assert.Contains(packageRegistry.GetPackages(), package => package.PackageId == TutorialPackageIds.TeamInfoBasic);

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
            Assert.NotNull(navigationStep.AfterCompleteAsync);
        }

        var globalBanPackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.BpGlobalBanRecordBasic);
        var globalBanStep = Assert.Single(globalBanPackage.Steps);
        Assert.Equal(TutorialTargetKind.ElementTag, globalBanStep.TargetKind);
        Assert.Equal(TutorialTargetNames.CurrentSurvivorGlobalBanRecordPanel, globalBanStep.TargetKey);
        Assert.Equal(ProductTourInteractionMode.AllowAll, globalBanStep.InteractionMode);
        Assert.Equal(new Point(80, 0), globalBanStep.CardOffset);
        Assert.Null(globalBanStep.WaitForSignalId);

        var playerManagePackage = Assert.Single(
            packageRegistry.GetPackages(),
            package => package.PackageId == TutorialPackageIds.TeamInfoPlayerManage);
        var playerManageStep = Assert.Single(playerManagePackage.Steps);
        Assert.Equal(TutorialTargetNames.HomePlayerPositionPanel, playerManageStep.TargetName);
        Assert.Equal(ProductTourInteractionMode.AllowTargetOnly, playerManageStep.InteractionMode);
        Assert.Equal(ProductTourAvatarPlacement.BottomRight, playerManageStep.AvatarPlacement);
        Assert.Equal(TutorialAvatarPose.LeftTop, playerManageStep.AvatarPose);

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
        Assert.Equal(TutorialTargetNames.AwayTeamInfoCard, jsonPresetPackage.Steps[2].ScrollAnchorName);
        Assert.All(
            jsonPresetPackage.Steps.Where(step => step.TargetName is TutorialTargetNames.HomePlayerListPanel or TutorialTargetNames.AwayPlayerListPanel),
            step =>
            {
                Assert.Equal(ProductTourInteractionMode.AllowTargetOnly, step.InteractionMode);
                Assert.Equal(ProductTourAvatarPlacement.BottomRight, step.AvatarPlacement);
                Assert.Equal(TutorialAvatarPose.LeftTop, step.AvatarPose);
            });
    }

    [Fact]
    public async Task SmartBpPackageCanRunFollowsCurrentPageDataContextModuleLoadedState()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var packages = CreateRegisteredPackages();
            var shell = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpModuleShell);
            var overview = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpModuleContentOverview);
            var capture = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpCaptureBasic);
            var region = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpRegionEditorBasic);
            var fullBpFlow = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpFullBpFlowBasic);
            var postGamePackage = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.SmartBpPostGameAutoFill);
            var provider = new ViewModelServiceProvider(new SmartBpPageViewModel { IsModuleLoaded = true });
            var unloadedOwner = new System.Windows.Controls.Grid
            {
                DataContext = new SmartBpPageViewModel { IsModuleLoaded = false }
            };
            var loadedOwner = new System.Windows.Controls.Grid
            {
                DataContext = new SmartBpPageViewModel { IsModuleLoaded = true }
            };
            var loadedOwnerWithContent = CreateSmartBpOwnerWithContent("SmartBpModuleContentHost");
            var captureOwner = CreateSmartBpOwnerWithContent("SmartBpModuleContentHost", SmartBpPage.TutorialTargets.StartCaptureButton);
            var regionOwner = CreateSmartBpOwnerWithContent("SmartBpModuleContentHost", SmartBpPage.TutorialTargets.RegionListPanel);
            var fullBpOwner = CreateSmartBpOwnerWithContent("SmartBpModuleContentHost", SmartBpPage.TutorialTargets.StartFullBpFlowButton);

            Assert.NotNull(shell.CanRunWithOwner);
            Assert.True(shell.CanRunWithOwner!(provider, unloadedOwner));
            Assert.False(shell.CanRunWithOwner!(provider, loadedOwner));

            Assert.NotNull(overview.CanRunWithOwner);
            Assert.False(overview.CanRunWithOwner!(provider, unloadedOwner));
            Assert.False(overview.CanRunWithOwner!(provider, loadedOwner));
            Assert.True(overview.CanRunWithOwner!(provider, loadedOwnerWithContent));

            Assert.NotNull(capture.CanRunWithOwner);
            Assert.False(capture.CanRunWithOwner!(provider, loadedOwner));
            Assert.True(capture.CanRunWithOwner!(provider, captureOwner));

            Assert.NotNull(region.CanRunWithOwner);
            Assert.False(region.CanRunWithOwner!(provider, loadedOwner));
            Assert.True(region.CanRunWithOwner!(provider, regionOwner));

            Assert.NotNull(fullBpFlow.CanRunWithOwner);
            Assert.False(fullBpFlow.CanRunWithOwner!(provider, loadedOwner));
            Assert.True(fullBpFlow.CanRunWithOwner!(provider, fullBpOwner));

            Assert.NotNull(postGamePackage.CanRunWithOwner);
            Assert.False(postGamePackage.CanRunWithOwner!(provider, unloadedOwner));
            Assert.False(postGamePackage.CanRunWithOwner!(provider, loadedOwner));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void DesignerLayoutEditPreviewStepTargetsDesignerSurfaceFrame()
    {
        var package = Assert.Single(
            CreateRegisteredPackages(),
            package => package.PackageId == TutorialPackageIds.DesignerV3LayoutEditBasic);
        var previewStep = Assert.Single(package.Steps, step => step.Title == "预览画布");

        Assert.Equal(TutorialTargetNames.PreviewZoomHost, previewStep.TargetName);
        Assert.NotEqual(TutorialTargetNames.PreviewCanvas, previewStep.TargetName);
    }

    [Fact]
    public void DesignerTutorialStartsWithWelcomeAndEndsWithHelpButton()
    {
        var packages = CreateRegisteredPackages();
        var overview = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.DesignerV3Overview);
        var help = Assert.Single(packages, package => package.PackageId == TutorialPackageIds.DesignerV3HelpBasic);

        Assert.Null(overview.Steps[0].TargetName);
        Assert.Contains("欢迎来到 v3 编辑器", overview.Steps[0].Title, StringComparison.Ordinal);
        Assert.Contains("详细修改前台界面", overview.Steps[0].Description, StringComparison.Ordinal);
        Assert.DoesNotContain(overview.Steps, step => step.TargetName == TutorialTargetNames.BehaviorPanelHost);

        var layoutSteps = packages.Single(package => package.PackageId == TutorialPackageIds.DesignerV3LayoutEditBasic).Steps;
        var previewStep = Assert.Single(layoutSteps, step => step.TargetName == TutorialTargetNames.PreviewZoomHost);
        Assert.Contains("点击画布上的一个控件", previewStep.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(layoutSteps, step => step.TargetName == TutorialTargetNames.InteractionLayer);
        Assert.DoesNotContain(layoutSteps.Select(step => step.Title), title => title.Contains("交互层", StringComparison.Ordinal));

        var propertySteps = packages.Single(package => package.PackageId == TutorialPackageIds.DesignerV3PropertyPanelBasic).Steps;
        Assert.Contains(propertySteps, step => step.Title == "行为和动画入口");

        var finalStep = Assert.Single(help.Steps);
        Assert.Equal(TutorialTargetNames.DesignerHelpButton, finalStep.TargetName);
        Assert.Contains("v3 编辑器的详细说明", finalStep.Description, StringComparison.Ordinal);
        Assert.False(finalStep.AllowMissingTarget);
        Assert.NotNull(finalStep.BeforeShowAsync);
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
            "DropZone",
            "LayerTopDropZone",
            "LayerBottomDropZone"
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
    public async Task FrontManageChildTutorialHelperResolvesVisibleChildOwnerAndPageKey()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var windowsView = new FrontedWindowsView();
            var root = new Grid { Children = { windowsView } };
            var window = new Window { Content = root, Width = 320, Height = 240 };
            window.Show();
            try
            {
                Assert.True(FrontManagePage.TryResolveCurrentChildTutorial(root, out var owner, out var pageKey));
                Assert.Same(windowsView, owner);
                Assert.Equal(FrontedWindowsView.TutorialPageKey, pageKey);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
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
        var package = Assert.Single(
            CreateRegisteredPackages(),
            package => package.PackageId == TutorialPackageIds.TeamInfoJsonImportPreset);
        Assert.Equal(4, package.Steps.Count);

        var expectedDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples");

        var homeImportStep = package.Steps[0];
        Assert.NotNull(homeImportStep.BeforeShowAsync);
        await homeImportStep.BeforeShowAsync!(new EmptyServiceProvider(), CancellationToken.None);
        var homeHint = TutorialFilePickerHints.ConsumeNextJsonPickerHint();
        Assert.Equal(expectedDirectory, homeHint.InitialDirectory);
        Assert.Contains("队伍信息导入示例-Wolves.json", homeHint.Title);

        var awayImportStep = package.Steps[2];
        Assert.NotNull(awayImportStep.BeforeShowAsync);
        await awayImportStep.BeforeShowAsync!(new EmptyServiceProvider(), CancellationToken.None);
        var awayHint = TutorialFilePickerHints.ConsumeNextJsonPickerHint();
        Assert.Equal(expectedDirectory, awayHint.InitialDirectory);
        Assert.Contains("队伍信息导入示例-GR.json", awayHint.Title);

        Assert.Null(TutorialFilePickerHints.ConsumeNextJsonPickerHint().InitialDirectory);
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

    private static Grid CreateSmartBpOwnerWithContent(string contentHostName, string? dynamicTargetName = null)
    {
        var moduleContent = new StackPanel();
        if (!string.IsNullOrWhiteSpace(dynamicTargetName))
        {
            moduleContent.Children.Add(new Button { Name = dynamicTargetName });
        }

        return new Grid
        {
            DataContext = new SmartBpPageViewModel { IsModuleLoaded = true },
            Children =
            {
                new ContentControl
                {
                    Name = contentHostName,
                    Content = moduleContent
                }
            }
        };
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        object? IServiceProvider.GetService(Type serviceType) => null;
    }

    private sealed class ViewModelServiceProvider(SmartBpPageViewModel viewModel) : IServiceProvider
    {
        object? IServiceProvider.GetService(Type serviceType) =>
            serviceType == typeof(SmartBpPageViewModel) ? viewModel : null;
    }
}
