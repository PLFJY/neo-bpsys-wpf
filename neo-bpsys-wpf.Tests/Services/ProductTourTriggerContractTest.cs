using System;
using System.IO;
using neo_bpsys_wpf.Tutorial;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>测试产品导览的触发器所有权和显式用户动作门控。</summary>
public sealed class ProductTourTriggerContractTest
{
    [Fact]
    public void LayoutPackagesView_LoadedAndVisible_ShouldRunOwnSequence()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Pages", "FrontManage", "FrontedLayoutPackagesView.xaml.cs");

        Assert.Contains("Loaded +=", source);
        Assert.Contains("IsVisibleChanged +=", source);
        Assert.Contains("DispatcherPriority.ContextIdle", source);
        Assert.Contains("DispatcherPriority.Render", source);
        Assert.Contains("Window.GetWindow(this) is not { IsVisible: true }", source);
        // The token must be captured into a local before awaiting so the catch
        // filter checks the same token that was handed to the awaited operations,
        // not a potentially-replaced _tutorialLifetime field. See the comment in
        // RunTutorialWhenVisibleAsync for the race condition this prevents.
        Assert.Contains("var token = _tutorialLifetime.Token", source);
        Assert.Contains("RunSequenceAsync(this, TutorialPageKey, token)", source);
        Assert.Contains("_tutorialTask is { IsCompleted: false }", source);
    }

    [Fact]
    public void PropertyPanel_ShouldNotRunDuringInitialLayoutRestore()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs");

        Assert.Contains("_viewModel?.IsRestoringSnapshotVisuals == true", source);
        Assert.Contains("_initialLayoutLoaded", source);
        Assert.Contains("isUserSelection", source);
        Assert.DoesNotContain("wasNull && isNowNonNull", source);
    }

    [Fact]
    public void BehaviorPanel_ShouldNotRunOnDataContextChangedOrVisibilityOnly()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "FrontedDesigner", "BehaviorPanelView.xaml.cs");

        Assert.DoesNotContain("IsVisibleChanged +=", source);
        Assert.DoesNotContain("ScheduleTutorialRun", source);
        Assert.DoesNotContain("RunSequenceAsync(this, TutorialPageKey", source);
        Assert.Contains("DataContextChanged += OnDataContextChanged", source);
    }

    [Fact]
    public void BehaviorPanel_ShouldRequireSelectedControlAndOuterExpanderExpanded()
    {
        var source = ReadRepoFile("neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml.cs");

        Assert.Contains("BehaviorExpander_OnExpanded", source);
        Assert.Contains("_viewModel?.SelectedDesignItem != null", source);
        Assert.Contains("BehaviorExpander.IsExpanded", source);
        Assert.Contains("BehaviorPanelHost.IsVisible", source);
        Assert.Contains("HasSelectedControl: true", source);
        Assert.Contains("DispatcherPriority.ContextIdle", source);
        Assert.Contains("DispatcherPriority.Render", source);
    }

    [Fact]
    public void BehaviorPanelHelpBasic_ShouldRunAfterExplicitExpansion()
    {
        var packages = new neo_bpsys_wpf.ProductTour.TutorialPackageRegistry();
        var sequences = new neo_bpsys_wpf.ProductTour.TutorialSequenceRegistry();
        var flows = new neo_bpsys_wpf.ProductTour.TutorialFlowRegistry();
        NeoBpsysTutorialRegistration.Register(packages, sequences, flows);

        var sequence = sequences.GetSequence(TutorialPageKeys.DesignerV3BehaviorPanel);
        Assert.Equal(TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic, sequence[^1]);
        var package = Assert.Single(
            packages.GetPackages(),
            definition => definition.PackageId == TutorialPackageIds.DesignerV3BehaviorPanelHelpBasic);
        Assert.Equal(TutorialTargetNames.BehaviorHelpButton, Assert.Single(package.Steps).TargetName);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. parts]));
    }
}
