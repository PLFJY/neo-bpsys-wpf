#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class LegacyAnimationMigrationTest
{
    [Fact]
    public void ProductionCodeDoesNotReferenceLegacyAnimationService()
    {
        var repositoryRoot = GetRepositoryRoot();
        var productionRoots = new[]
        {
            Path.Combine(repositoryRoot, "neo-bpsys-wpf"),
            Path.Combine(repositoryRoot, "neo-bpsys-wpf.Core")
        };

        var offenders = productionRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => File.ReadAllText(path).Contains("IAnimationService", StringComparison.Ordinal)
                           || File.ReadAllText(path).Contains("class AnimationService", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void PickPageDoesNotExposeLegacyPickingBorderSwitch()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "neo-bpsys-wpf",
            "ViewModels",
            "Pages",
            "PickPageViewModel.cs"));

        Assert.DoesNotContain("PickingBorderSwitch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SurPickingBorderList", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HunPickingBorder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StartPickingBorderBreathingAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StopPickingBorderBreathingAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInBpBehaviorDocumentContainsPickSwapAndPickingBorderBehaviors()
    {
        var repositoryRoot = GetRepositoryRoot();
        var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(
            File.ReadAllText(Path.Combine(repositoryRoot, "neo-bpsys-wpf", "Resources", "FrontedLayouts", "BpWindow.json")))!;
        var behaviors = JsonSerializer.Deserialize<FrontedBehaviorDocument>(
            File.ReadAllText(Path.Combine(repositoryRoot, "neo-bpsys-wpf", "Resources", "FrontedBehaviors", "BpWindow.behaviors.json")))!;

        foreach (var name in new[] { "SurPick0", "SurPick1", "SurPick2", "SurPick3", "HunPick" })
        {
            var control = layout.ControlLayout.Controls[name];
            var set = Assert.Single(behaviors.ControlBehaviorSets, item => item.DisplayName == name);
            Assert.NotEqual(Guid.Empty, control.BehaviorGuid);
            Assert.Equal(control.BehaviorGuid, set.BehaviorGuid);
            Assert.All(
                set.Behaviors
                    .SelectMany(behavior => new[]
                    {
                        behavior.Graph,
                        behavior.ExitGraph,
                        behavior.EnterGraph,
                        behavior.StartGraph,
                        behavior.LoopGraph,
                        behavior.StopGraph
                    })
                    .SelectMany(graph => graph.Nodes)
                    .Select(node => node.Properties.TryGetValue("Target", out var target)
                        ? target.ToString()
                        : null)
                    .Where(target => !string.IsNullOrWhiteSpace(target)),
                target => Assert.True(
                    target == "Self"
                    || target == $"part:{set.BehaviorGuid}:PickingBorder",
                    $"Unexpected built-in animation target: {target}"));
            Assert.Contains(set.Behaviors, behavior =>
                behavior.Kind == FrontedBehaviorKind.Transition
                && behavior.TransitionTrigger?.EventType == "Selection.CharacterPick");
            Assert.Contains(set.Behaviors, behavior =>
                behavior.Kind == FrontedBehaviorKind.Loop
                && behavior.StartTrigger?.EventType == "Guidance.StepChanged");
        }

        foreach (var name in new[] { "SurPick0", "SurPick1", "SurPick2", "SurPick3" })
        {
            var set = Assert.Single(behaviors.ControlBehaviorSets, item => item.DisplayName == name);
            Assert.Equal(2, set.Behaviors.Count(behavior =>
                behavior.Kind == FrontedBehaviorKind.Transition
                && behavior.TransitionTrigger?.EventType == "Selection.CharacterSwap"));
        }

        var validator = new FrontedNodeGraphValidator();
        var graphs = behaviors.ControlBehaviorSets
            .SelectMany(set => set.Behaviors)
            .SelectMany(behavior => new[]
            {
                behavior.ExitGraph,
                behavior.EnterGraph,
                behavior.StartGraph,
                behavior.LoopGraph,
                behavior.StopGraph
            })
            .Where(graph => graph.Nodes.Count > 0);
        Assert.All(graphs, graph =>
            Assert.DoesNotContain(
                validator.Validate(graph),
                message => message.Severity == FrontedNodeGraphValidationSeverity.Error));
        Assert.All(graphs, graph =>
            Assert.DoesNotContain(graph.Nodes, node => node.NodeType == "flow.delay"));
        Assert.All(graphs.SelectMany(graph => graph.Nodes), node =>
            Assert.InRange(node.X, 500, 3000));
        Assert.All(graphs.SelectMany(graph => graph.Nodes), node =>
            Assert.InRange(node.Y, 400, 900));
    }

    [Fact]
    public async Task BehaviorServiceLoadsBuiltInBehaviorDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), "neo-bpsys-built-in-behavior-tests", Guid.NewGuid().ToString("N"));
        var resourcesRoot = Path.Combine(root, "Resources");
        var layoutsRoot = Path.Combine(resourcesRoot, "FrontedLayouts");
        var behaviorsRoot = Path.Combine(resourcesRoot, "FrontedBehaviors");
        Directory.CreateDirectory(layoutsRoot);
        Directory.CreateDirectory(behaviorsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(behaviorsRoot, "BpWindow.behaviors.json"),
            """{"Version":1,"WindowType":"BpWindow","CanvasName":"BaseCanvas","ControlBehaviorSets":[{"BehaviorGuid":"a0000000-0000-0000-0000-000000000001","DisplayName":"SurPick0","Behaviors":[]}]}""",
            TestContext.Current.CancellationToken);

        try
        {
            var manager = new FrontedLayoutPackageManager(Path.Combine(root, "packages"), layoutsRoot);
            var service = new FrontedBehaviorService(
                new FrontedUserLayoutStore(Path.Combine(root, "user")),
                manager,
                NullLogger<FrontedBehaviorService>.Instance);

            var document = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            Assert.Single(document.ControlBehaviorSets);
            Assert.Equal("SurPick0", document.ControlBehaviorSets[0].DisplayName);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DuplicatingBuiltInPackageCopiesDefaultBehaviorDocuments()
    {
        var root = Path.Combine(Path.GetTempPath(), "neo-bpsys-built-in-behavior-copy-tests", Guid.NewGuid().ToString("N"));
        var resourcesRoot = Path.Combine(root, "Resources");
        var layoutsRoot = Path.Combine(resourcesRoot, "FrontedLayouts");
        var behaviorsRoot = Path.Combine(resourcesRoot, "FrontedBehaviors");
        Directory.CreateDirectory(layoutsRoot);
        Directory.CreateDirectory(behaviorsRoot);
        await File.WriteAllTextAsync(Path.Combine(layoutsRoot, "BpWindow.json"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(behaviorsRoot, "BpWindow.behaviors.json"), "{}", TestContext.Current.CancellationToken);

        try
        {
            var manager = new FrontedLayoutPackageManager(Path.Combine(root, "packages"), layoutsRoot);

            var package = await manager.DuplicatePackageAsync(
                FrontedLayoutPackageManager.BuiltInPackageId,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(File.Exists(Path.Combine(package.InstallPath, "behaviors", "BpWindow.behaviors.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string GetRepositoryRoot([CallerFilePath] string sourceFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
}
