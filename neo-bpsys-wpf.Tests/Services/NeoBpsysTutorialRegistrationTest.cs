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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task HostRegistrationDoesNotOwnModuleTutorialPackages()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var packages = CreateRegisteredPackages();
            Assert.DoesNotContain(packages, package => package.PageKey == TutorialPageKeys.SmartBp);

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
    public void DesignerBaseSequence_ShouldEndWithHelp()
    {
        var packages = CreateRegisteredPackages();
        var help = Assert.Single(
            packages,
            package => package.PackageId == TutorialPackageIds.DesignerV3HelpBasic);

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
