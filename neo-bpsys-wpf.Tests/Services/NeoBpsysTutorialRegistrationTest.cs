using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Tutorial;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        Assert.Equal(43, packageRegistry.GetPackages().Count);

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
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3LayoutEditBasic, firstRun.IncludedPackageIds);
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
    public async Task TeamInfoJsonImportPresetUsesExamplesDirectoryForCommonJsonPicker()
    {
        var package = Assert.Single(
            NeoBpsysTutorialPackages.CreatePackages(),
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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        object? IServiceProvider.GetService(Type serviceType) => null;
    }
}
