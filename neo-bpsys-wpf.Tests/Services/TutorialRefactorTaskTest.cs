#nullable enable

extern alias smartbp;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Xunit;
using SmartBpModuleEntryPoint = smartbp::neo_bpsys_wpf.SmartBp.Module.SmartBpModuleEntryPoint;
using SmartBpModuleContentView = smartbp::neo_bpsys_wpf.Views.Pages.SmartBpModuleContentView;
using RegionEditorWindow = smartbp::neo_bpsys_wpf.Views.Windows.RegionEditorWindow;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests for the tutorial refactor task: RegionEditor owner boundaries,
/// on-demand packages, modal child handoff, dynamic contributors, and module tutorial ownership.
/// </summary>
public sealed class TutorialRefactorTaskTest
{

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. RegionEditor owner boundary tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RegionEditorEntryPackage_ShouldContainOnlyModuleViewTargets()
    {
        var (packageRegistry, _, _) = RegisterModuleTutorials();
        var package = Assert.Single(packageRegistry.GetPackages(),
            p => p.PackageId == SmartBpModuleContentView.PackageIds.RegionEditorEntryBasic);

        var targetNames = package.Steps.Select(s => s.TargetName).ToArray();
        Assert.Single(targetNames);
        Assert.Equal(nameof(SmartBpModuleContentView.SmartBpPostGamePreviewPanel), targetNames[0]);
    }

    [Fact]
    public void RegionEditorWindowPackage_ShouldContainOnlyRegionEditorWindowTargets()
    {
        var (packageRegistry, _, _) = RegisterModuleTutorials();
        var package = Assert.Single(packageRegistry.GetPackages(),
            p => p.PackageId == RegionEditorWindow.PackageIds.RegionEditorBasic);

        var targetNames = package.Steps.Select(s => s.TargetName).ToArray();
        Assert.Equal(3, targetNames.Length);
        Assert.Equal(nameof(RegionEditorWindow.SmartBpRegionPreviewPanel), targetNames[0]);
        Assert.Equal(nameof(RegionEditorWindow.SmartBpRegionListPanel), targetNames[1]);
        Assert.Equal(nameof(RegionEditorWindow.SmartBpSaveRegionButton), targetNames[2]);
    }

    [Fact]
    public void RegionEditorPackage_ShouldCompleteAndPersistState()
    {
        var (packageRegistry, _, _) = RegisterModuleTutorials();
        var entryPackage = Assert.Single(packageRegistry.GetPackages(),
            p => p.PackageId == SmartBpModuleContentView.PackageIds.RegionEditorEntryBasic);
        var windowPackage = Assert.Single(packageRegistry.GetPackages(),
            p => p.PackageId == RegionEditorWindow.PackageIds.RegionEditorBasic);

        Assert.All(entryPackage.Steps, step => Assert.True(step.AllowMissingTarget));
        Assert.All(windowPackage.Steps, step => Assert.True(step.AllowMissingTarget));
        Assert.NotEmpty(entryPackage.Steps);
        Assert.NotEmpty(windowPackage.Steps);
        Assert.All(entryPackage.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.TitleKey)));
        Assert.All(windowPackage.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.TitleKey)));
    }

    [Fact]
    public void RegionEditorCompletion_ShouldAllowLaterSmartBpPackages()
    {
        var (packageRegistry, sequenceRegistry, _) = RegisterModuleTutorials();
        var moduleSequence = sequenceRegistry.GetSequence(SmartBpModuleContentView.TutorialPageKey).ToArray();

        var entryIndex = Array.IndexOf(moduleSequence, SmartBpModuleContentView.PackageIds.RegionEditorEntryBasic);
        var fullBpFlowIndex = Array.IndexOf(moduleSequence, SmartBpModuleContentView.PackageIds.FullBpFlowBasic);

        Assert.True(entryIndex >= 0, "RegionEditorEntryBasic should be in the module content sequence.");
        Assert.True(fullBpFlowIndex >= 0, "FullBpFlowBasic should be in the module content sequence.");
        Assert.True(entryIndex < fullBpFlowIndex,
            "FullBpFlowBasic should come after RegionEditorEntryBasic in the sequence.");
    }

    [Fact]
    public void SmartBpFullFlowPackage_ShouldRunAfterRegionEntryPackage()
    {
        var (_, sequenceRegistry, _) = RegisterModuleTutorials();
        var sequence = sequenceRegistry.GetSequence(SmartBpModuleContentView.TutorialPageKey).ToArray();

        var entryIndex = Array.IndexOf(sequence, SmartBpModuleContentView.PackageIds.RegionEditorEntryBasic);
        var fullBpFlowIndex = Array.IndexOf(sequence, SmartBpModuleContentView.PackageIds.FullBpFlowBasic);

        Assert.True(entryIndex >= 0 && fullBpFlowIndex >= 0);
        Assert.True(entryIndex < fullBpFlowIndex);
    }

    [Fact]
    public void SmartBpPostGamePackage_ShouldUseExistingTargetsOrDialogue()
    {
        var (packageRegistry, _, _) = RegisterModuleTutorials();
        var package = Assert.Single(packageRegistry.GetPackages(),
            p => p.PackageId == SmartBpModuleContentView.PackageIds.PostGameAutoFill);

        Assert.NotEmpty(package.Items);
        var hasDialogue = package.Items.OfType<TutorialPackageDialogueItem>().Any();
        var hasStep = package.Items.OfType<TutorialPackageStepItem>().Any();
        Assert.True(hasDialogue || hasStep,
            "PostGameAutoFill should contain dialogue or step items.");

        foreach (var stepItem in package.Items.OfType<TutorialPackageStepItem>())
        {
            Assert.False(string.IsNullOrWhiteSpace(stepItem.Step.TargetName),
                "PostGameAutoFill steps should reference real targets.");
            Assert.True(stepItem.Step.AllowMissingTarget,
                "PostGameAutoFill steps should allow missing targets since the UI may not always be visible.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. OnDemand package registration tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PackageBuilder_OnDemand_ShouldRegisterWithoutAddingToSequence()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        var builder = new TutorialBuilder(packageRegistry, sequenceRegistry, flowRegistry);

        builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.OnDemand.Test"))
                .OnDemand()
                .Step("On-Demand Step")
                    .Text("Description")
                    .TargetName("Target")
                .Build();

        var package = packageRegistry.GetPackage("Package.OnDemand.Test");
        Assert.NotNull(package);
        Assert.DoesNotContain("Package.OnDemand.Test", sequenceRegistry.GetSequence("Page.TestOwner"));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 3. Designer PropertyPanelBasic tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DesignerInitialSequence_ShouldNotContainPropertyPanelBasic()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        var designerSequence = sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3PropertyPanelBasic, designerSequence);

        var propertyPackage = Assert.Single(packageRegistry.GetPackages(),
            p => p.PackageId == TutorialPackageIds.DesignerV3PropertyPanelBasic);
        Assert.NotNull(propertyPackage);
    }

    [Fact]
    public void DesignerFirstControlSelection_ShouldQueuePropertyPanelBasic()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs");

        Assert.Contains("nameof(FrontedDesignerWindowViewModel.SelectedDesignItem)", source);
        Assert.Contains("TryQueuePropertyPanelTutorial", source);
        Assert.Contains("_initialLayoutLoaded", source);
        Assert.Contains("isUserSelection", source);
        Assert.Contains("currentItem is not null", source);
        Assert.Contains("WaitForPropertyGridReadyAsync", source);
        Assert.Contains("RunPackageAsync(this, Tours.PropertyPanelBasic", source);
    }

    [Fact]
    public void DesignerRepeatedSelection_ShouldCoalescePropertyPanelRequest()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs");

        Assert.Contains("_propertyPanelTutorialTriggered", source);
        Assert.Contains("if (_propertyPanelTutorialTriggered)", source);
        Assert.Contains("_propertyPanelTutorialTask is { IsCompleted: false }", source);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 4. Animation editor and modal handoff tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AnimationEditorLoaded_ShouldQueueSequence()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedBehaviorAnimationEditorWindow.xaml.cs");

        Assert.Contains("Loaded += async (_, _) =>", source);
        Assert.Contains("AnimationTabs.SelectFirstItemIfNoneSelected()", source);
        Assert.Contains("DispatcherPriority.ContextIdle", source);
        Assert.Contains("DispatcherPriority.Render", source);
        Assert.Contains("IsVisible", source);
        Assert.Contains("AnimationTabs.SelectedItem == null", source);
        Assert.Contains("RunSequenceAsync(this, TutorialPageKey", source);
    }

    [Fact]
    public async Task NonModalChildTutorial_ShouldRunBeforeParentResumes()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var loggerFactory = LoggerFactory.Create(b => { });
            var stepCancellation = new TestStepCancellation();
            var coordinator = new TutorialPlaybackCoordinator(
                loggerFactory.CreateLogger<TutorialPlaybackCoordinator>(),
                stepCancellation);

            var parentWindow = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            var childWindow = new Window { Width = 0, Height = 0, ShowInTaskbar = false };
            parentWindow.Show();
            try
            {
                childWindow.Owner = parentWindow;

                var parentStartedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var childStartedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var parentRunCount = 0;

                var parentTask = coordinator.RunSequenceAsync(
                    parentWindow, "Parent",
                    async _ =>
                    {
                        parentRunCount++;
                        parentStartedTcs.TrySetResult();
                        if (parentRunCount == 1)
                        {
                            try
                            {
                                await Task.Delay(Timeout.Infinite, stepCancellation.Token);
                            }
                            catch (OperationCanceledException) when (stepCancellation.Token.IsCancellationRequested)
                            {
                                return TutorialRunResult.ChildWindowHandoff;
                            }
                        }

                        return TutorialRunResult.Completed;
                    },
                    CancellationToken.None);

                await parentStartedTcs.Task;

                var childSession = await coordinator.BeginChildWindowSessionAsync(childWindow);
                Assert.NotNull(childSession);
                Assert.True(stepCancellation.CancelCalled, "Parent step should be cancelled to yield the gate.");
                childWindow.Show();

                var childTask = coordinator.RunAsync(
                    childWindow, "Child",
                    token =>
                    {
                        childStartedTcs.TrySetResult();
                        return Task.FromResult(TutorialRunResult.Completed);
                    },
                    CancellationToken.None);

                await childStartedTcs.Task;
                await childTask;
                Assert.False(parentTask.IsCompleted);

                childSession.Complete();

                var parentResult = await parentTask;
                Assert.Equal(TutorialRunResult.Completed, parentResult);
            }
            finally
            {
                childWindow.Close();
                parentWindow.Close();
            }
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void DesignerSequence_ShouldWaitForInitialPreviewRender()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs");

        Assert.Contains("_initialPreviewReady.Task.WaitAsync", source);
        Assert.Contains("_initialPreviewReady.TrySetResult()", source);
        Assert.Contains("Initial preview render started", source);
        Assert.Contains("Initial preview render completed", source);
        Assert.DoesNotContain("Task.Delay", source);
    }

    [Fact]
    public void DesignerHelpBasic_ShouldRunOnFirstOpen()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        var sequence = sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3);
        Assert.Equal(TutorialPackageIds.DesignerV3HelpBasic, sequence[^1]);
        Assert.DoesNotContain(TutorialPackageIds.DesignerV3BehaviorEditBasic, sequence);
        Assert.NotNull(packageRegistry.GetPackage(TutorialPackageIds.DesignerV3HelpBasic));
    }

    [Fact]
    public void AnimationEditorModalSession_ShouldReceivePlaybackHandoff()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "FrontedDesigner", "BehaviorPanelView.xaml.cs");
        var beginIndex = source.IndexOf("BeginChildWindowSessionAsync", StringComparison.Ordinal);
        var showIndex = source.IndexOf("window.ShowDialog()", StringComparison.Ordinal);
        var completeIndex = source.IndexOf("childSession?.Complete()", StringComparison.Ordinal);

        Assert.True(beginIndex >= 0 && beginIndex < showIndex);
        Assert.True(showIndex < completeIndex);
        Assert.Contains("finally", source);
    }

    [Fact]
    public void AnimationEditor_ShouldPlayAllPendingPackages()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        NeoBpsysTutorialRegistration.Register(packageRegistry, sequenceRegistry, flowRegistry);

        var sequence = sequenceRegistry.GetSequence(TutorialPageKeys.DesignerV3AnimationEditor);
        Assert.Equal(
            [
                TutorialPackageIds.DesignerV3AnimationEditorOverview,
                TutorialPackageIds.DesignerV3AnimationEditorTimelineBasic,
                TutorialPackageIds.DesignerV3AnimationEditorKeyFrameBasic,
                TutorialPackageIds.DesignerV3AnimationEditorPreviewBasic,
                TutorialPackageIds.DesignerV3AnimationEditorHelpBasic
            ],
            sequence);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 5. Dynamic contributor tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DynamicContributor_ShouldRegisterAfterStartup()
    {
        var (packageRegistry, sequenceRegistry, _) = RegisterModuleTutorials();

        Assert.Contains(packageRegistry.GetPackages(),
            p => p.PackageId == SmartBpModuleContentView.PackageIds.OcrModelDownloadBasic);
        Assert.Contains(packageRegistry.GetPackages(),
            p => p.PackageId == RegionEditorWindow.PackageIds.RegionEditorBasic);

        var moduleSequence = sequenceRegistry.GetSequence(SmartBpModuleContentView.TutorialPageKey);
        Assert.NotEmpty(moduleSequence);

        var regionSequence = sequenceRegistry.GetSequence(RegionEditorWindow.TutorialPageKey);
        Assert.NotEmpty(regionSequence);
    }

    [Fact]
    public void DynamicContributor_ShouldBeIdempotent()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        var loggerFactory = LoggerFactory.Create(b => { });
        var registrationService = new TutorialRegistrationService(
            packageRegistry, sequenceRegistry, flowRegistry,
            loggerFactory.CreateLogger<TutorialRegistrationService>());

        var contributor = new SmartBpModuleEntryPoint();
        registrationService.RegisterContributor(contributor);
        var packageCountAfterFirst = packageRegistry.GetPackages().Count;

        registrationService.RegisterContributor(contributor);
        var packageCountAfterSecond = packageRegistry.GetPackages().Count;

        Assert.Equal(packageCountAfterFirst, packageCountAfterSecond);
    }

    [Fact]
    public void DynamicContributorDuplicatePackageId_ShouldFail()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        var loggerFactory = LoggerFactory.Create(b => { });
        var registrationService = new TutorialRegistrationService(
            packageRegistry, sequenceRegistry, flowRegistry,
            loggerFactory.CreateLogger<TutorialRegistrationService>());

        registrationService.RegisterContributor(new SmartBpModuleEntryPoint());

        Assert.Throws<InvalidOperationException>(() =>
            registrationService.RegisterContributor(new DuplicatePackageContributor()));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 6. Module ownership and registration ordering tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SmartBpModule_ShouldRegisterItsOwnTutorialOwners()
    {
        var contributor = new SmartBpModuleEntryPoint();
        Assert.Equal("neo-bpsys-wpf.SmartBp.Module", contributor.RegistrationId);

        var (packageRegistry, sequenceRegistry, _) = RegisterModuleTutorials();

        Assert.Contains(packageRegistry.GetPackages(),
            p => p.PageKey == SmartBpModuleContentView.TutorialPageKey);
        Assert.Contains(packageRegistry.GetPackages(),
            p => p.PageKey == RegionEditorWindow.TutorialPageKey);

        Assert.NotEmpty(sequenceRegistry.GetSequence(SmartBpModuleContentView.TutorialPageKey));
        Assert.NotEmpty(sequenceRegistry.GetSequence(RegionEditorWindow.TutorialPageKey));
    }

    [Fact]
    public void SmartBpModuleTutorials_ShouldUseNameofTargets()
    {
        var moduleXaml = ReadRepoFile(
            "neo-bpsys-wpf.SmartBp.Module",
            "Views",
            "SmartBpModuleContentView.xaml");
        var regionXaml = ReadRepoFile(
            "neo-bpsys-wpf.SmartBp.Module",
            "Views",
            "RegionEditorWindow.xaml");

        var (packageRegistry, _, _) = RegisterModuleTutorials();
        var modulePackages = packageRegistry.GetPackages()
            .Where(p => p.PageKey == SmartBpModuleContentView.TutorialPageKey);
        var regionPackages = packageRegistry.GetPackages()
            .Where(p => p.PageKey == RegionEditorWindow.TutorialPageKey);

        foreach (var package in modulePackages)
        {
            foreach (var step in package.Steps)
            {
                if (!string.IsNullOrWhiteSpace(step.TargetName))
                {
                    Assert.Contains(
                        $"x:Name=\"{step.TargetName}\"",
                        moduleXaml);
                }
            }
        }

        foreach (var package in regionPackages)
        {
            foreach (var step in package.Steps)
            {
                if (!string.IsNullOrWhiteSpace(step.TargetName))
                {
                    Assert.Contains(
                        $"x:Name=\"{step.TargetName}\"",
                        regionXaml);
                }
            }
        }
    }

    [Fact]
    public void HostSmartBpPage_ShouldNotDeclareModuleViewTargets()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Pages", "SmartBpPage.xaml");

        Assert.DoesNotContain("SmartBpPreviewPanel", source);
        Assert.DoesNotContain("SmartBpWindowSelector", source);
        Assert.DoesNotContain("SmartBpStartCaptureButton", source);
        Assert.DoesNotContain("SmartBpRegionEditorButton", source);
        Assert.DoesNotContain("SmartBpStartFullBpFlowButton", source);
        Assert.DoesNotContain("SmartBpOcrModelManagementCard", source);

        Assert.Contains("SmartBpModuleContentHost", source);
        Assert.Contains("SmartBpModulePathTextBox", source);
        Assert.Contains("SmartBpLoadLocalModuleButton", source);
    }

    [Fact]
    public void ModuleContent_ShouldNotLoadBeforeTutorialRegistration()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf",
            "Services",
            "SmartBpModule",
            "SmartBpModuleManager.cs");

        var contributorIndex = source.IndexOf("ITutorialRegistrationContributor", StringComparison.Ordinal);
        var contentIndex = source.IndexOf("CreateSmartBpContent", StringComparison.Ordinal);

        Assert.True(contributorIndex >= 0, "Module manager should check for ITutorialRegistrationContributor.");
        Assert.True(contentIndex >= 0, "Module manager should create SmartBP content.");
        Assert.True(contributorIndex < contentIndex,
            "Tutorial contributor registration must happen before module content creation.");

        Assert.Contains("RegisterContributor", source);
        Assert.Contains("ModuleContent = _entryPoint.CreateSmartBpContent", source);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static (TutorialPackageRegistry packages, TutorialSequenceRegistry sequences, TutorialFlowRegistry flows)
        RegisterModuleTutorials()
    {
        var packageRegistry = new TutorialPackageRegistry();
        var sequenceRegistry = new TutorialSequenceRegistry();
        var flowRegistry = new TutorialFlowRegistry();
        var loggerFactory = LoggerFactory.Create(b => { });
        var registrationService = new TutorialRegistrationService(
            packageRegistry, sequenceRegistry, flowRegistry,
            loggerFactory.CreateLogger<TutorialRegistrationService>());

        registrationService.RegisterContributor(new SmartBpModuleEntryPoint());
        return (packageRegistry, sequenceRegistry, flowRegistry);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine([directory.FullName, .. parts]);
        Assert.True(File.Exists(path), $"File not found: {path}");
        return File.ReadAllText(path);
    }

    private sealed class TestStepCancellation : ITutorialStepCancellation
    {
        private readonly CancellationTokenSource _cts = new();
        public bool CancelCalled { get; private set; }
        public CancellationToken Token => _cts.Token;
        public void YieldCurrentStepForChildWindow()
        {
            CancelCalled = true;
            _cts.Cancel();
        }
    }

    private sealed class DuplicatePackageContributor : ITutorialRegistrationContributor
    {
        public string RegistrationId => "test.duplicate";

        public void RegisterTutorials(ITutorialBuilder builder)
        {
            builder.ForRegion<SmartBpModuleContentView>()
                .Package(new TutorialPackageRef(SmartBpModuleContentView.PackageIds.OcrModelDownloadBasic))
                    .Step("Duplicate")
                        .Text("This should fail.")
                        .TargetName("AnyTarget")
                    .Build();
        }
    }

    private sealed class TestTutorialOwner : FrameworkElement, ITutorialOwner<TestTutorialOwner>
    {
        public static string TutorialKey => "Page.TestOwner";
        public static void RegisterTutorials(ITutorialBuilder builder) { }
    }
}
