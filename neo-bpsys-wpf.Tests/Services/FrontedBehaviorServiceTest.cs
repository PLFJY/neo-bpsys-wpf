#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedBehaviorServiceTest
{
    [Fact]
    public async Task BehaviorDocument_SaveLoad_RoundTrip()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var behaviorGuid = Guid.NewGuid();
            var document = new FrontedBehaviorDocument
            {
                WindowType = "BpWindow",
                CanvasName = "BaseCanvas"
            };
            document.GetOrCreateSet(behaviorGuid, "Title").Behaviors.Add(new FrontedBehavior
            {
                Name = "Fade",
                Trigger = new TriggerDescriptor
                {
                    EventType = "CharacterPicked",
                    Filters =
                    {
                        new TriggerFilter
                        {
                            Left = "Event.Camp",
                            Operator = TriggerFilterOperator.NotEquals,
                            Right = "Hun",
                            RightValueKind = TriggerFilterValueKind.EventPath
                        }
                    }
                }
            });

            await service.SaveDocumentAsync(document, TestContext.Current.CancellationToken);
            var loaded = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            var loadedBehavior = Assert.Single(loaded.FindSet(behaviorGuid)!.Behaviors);
            Assert.Equal("Fade", loadedBehavior.Name);
            var filter = Assert.Single(loadedBehavior.Trigger!.Filters);
            Assert.Equal(TriggerFilterOperator.NotEquals, filter.Operator);
            Assert.Equal(TriggerFilterValueKind.EventPath, filter.RightValueKind);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task BehaviorService_MissingFile_ReturnsEmptyDocument()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);

            var document = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            Assert.Equal(1, document.Version);
            Assert.Equal("BpWindow", document.WindowType);
            Assert.Equal("BaseCanvas", document.CanvasName);
            Assert.Empty(document.ControlBehaviorSets);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// 加载包含手填数值的行为文档时，不应补写数值输入单位。
    /// </summary>
    [Fact]
    public async Task BehaviorService_LoadDocument_PreservesLiteralNumericValuesWithoutInputUnits()
    {
        var root = CreateTempRoot();
        try
        {
            var behaviorsRoot = Path.Combine(root, "Resources", "FrontedBehaviors");
            Directory.CreateDirectory(behaviorsRoot);
            await File.WriteAllTextAsync(
                Path.Combine(behaviorsRoot, "BpWindow.behaviors.json"),
                """
                {
                  "Version": 1,
                  "WindowType": "BpWindow",
                  "CanvasName": "BaseCanvas",
                  "ControlBehaviorSets": [
                    {
                      "BehaviorGuid": "a0000000-0000-0000-0000-000000000001",
                      "Behaviors": [
                        {
                          "Graph": {
                            "Nodes": [
                              { "NodeType": "action.setProperty", "Properties": { "Target": "Self", "TargetLayer": "Control", "PropertyName": "Opacity", "Value": "0" } },
                              { "NodeType": "action.animateProperty", "Properties": { "Target": "Self", "TargetLayer": "Control", "PropertyName": "Opacity", "From": "0", "To": "1", "DurationMs": 250 } }
                            ]
                          }
                        }
                      ]
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var document = await CreateService(root).LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);
            var nodes = Assert.Single(document.ControlBehaviorSets).Behaviors.Single().Graph.Nodes;
            var set = Assert.Single(nodes, node => node.NodeType == "action.setProperty");
            var animate = Assert.Single(nodes, node => node.NodeType == "action.animateProperty");

            Assert.False(set.Properties.ContainsKey("ValueInputUnit"));
            Assert.False(animate.Properties.ContainsKey("FromInputUnit"));
            Assert.False(animate.Properties.ContainsKey("ToInputUnit"));
            Assert.DoesNotContain(
                new FrontedNodeGraphValidator().Validate(document.ControlBehaviorSets[0].Behaviors[0].Graph),
                message => message.Severity == FrontedNodeGraphValidationSeverity.Error);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    /// <summary>
    /// 加载转场行为文档时，不应改写退出和进入图中的手填数值。
    /// </summary>
    [Fact]
    public async Task BehaviorService_LoadTransition_PreservesLiteralNumericValuesWithoutInputUnits()
    {
        var root = CreateTempRoot();
        try
        {
            var behaviorsRoot = Path.Combine(root, "Resources", "FrontedBehaviors");
            Directory.CreateDirectory(behaviorsRoot);
            await File.WriteAllTextAsync(
                Path.Combine(behaviorsRoot, "BpWindow.behaviors.json"),
                """
                {
                  "Version": 1,
                  "WindowType": "BpWindow",
                  "CanvasName": "BaseCanvas",
                  "ControlBehaviorSets": [
                    {
                      "BehaviorGuid": "a0000000-0000-0000-0000-000000000001",
                      "Behaviors": [
                        {
                          "Kind": "Transition",
                          "ExitGraph": {
                            "Nodes": [
                              { "NodeType": "action.setProperty", "Properties": { "Target": "Self", "TargetLayer": "Control", "PropertyName": "Opacity", "Value": "0" } }
                            ]
                          },
                          "EnterGraph": {
                            "Nodes": [
                              { "NodeType": "action.animateProperty", "Properties": { "Target": "Self", "TargetLayer": "Control", "PropertyName": "Opacity", "From": "0", "To": "1", "DurationMs": 250 } }
                            ]
                          }
                        }
                      ]
                    }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);

            var behavior = Assert.Single((await CreateService(root)
                    .LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken))
                .ControlBehaviorSets)
                .Behaviors.Single();
            var exitSet = Assert.Single(behavior.ExitGraph.Nodes);
            var enterAnimate = Assert.Single(behavior.EnterGraph.Nodes);

            Assert.False(exitSet.Properties.ContainsKey("ValueInputUnit"));
            Assert.False(enterAnimate.Properties.ContainsKey("FromInputUnit"));
            Assert.False(enterAnimate.Properties.ContainsKey("ToInputUnit"));
            Assert.DoesNotContain(
                new FrontedNodeGraphValidator().Validate(behavior.ExitGraph)
                    .Concat(new FrontedNodeGraphValidator().Validate(behavior.EnterGraph)),
                message => message.Severity == FrontedNodeGraphValidationSeverity.Error);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task BehaviorService_RemoveBehaviors_RemovesSet()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var removedGuid = Guid.NewGuid();
            var keptGuid = Guid.NewGuid();
            var document = new FrontedBehaviorDocument
            {
                WindowType = "BpWindow",
                CanvasName = "BaseCanvas"
            };
            document.GetOrCreateSet(removedGuid, "Removed").Behaviors.Add(new FrontedBehavior { Name = "Removed" });
            document.GetOrCreateSet(keptGuid, "Kept").Behaviors.Add(new FrontedBehavior { Name = "Kept" });
            await service.SaveDocumentAsync(document, TestContext.Current.CancellationToken);

            var loaded = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);
            service.RemoveBehaviors(removedGuid);
            await service.SaveDocumentAsync(loaded, TestContext.Current.CancellationToken);
            var roundTrip = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            Assert.Null(roundTrip.FindSet(removedGuid));
            Assert.NotNull(roundTrip.FindSet(keptGuid));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void TriggerFilter_RoundTrip_PreservesOperatorAndValueKind()
    {
        var filter = new TriggerFilter
        {
            Left = "Event.Team",
            Operator = TriggerFilterOperator.Contains,
            Right = "HomeTeam",
            RightValueKind = TriggerFilterValueKind.BindingPath
        };

        var roundTrip = JsonSerializer.Deserialize<TriggerFilter>(JsonSerializer.Serialize(filter));

        Assert.NotNull(roundTrip);
        Assert.Equal(TriggerFilterOperator.Contains, roundTrip.Operator);
        Assert.Equal(TriggerFilterValueKind.BindingPath, roundTrip.RightValueKind);
    }

    private static FrontedBehaviorService CreateService(string root)
    {
        var resourcesRoot = Path.Combine(root, "Resources");
        var builtInLayoutsRoot = Path.Combine(resourcesRoot, "FrontedLayouts");
        Directory.CreateDirectory(builtInLayoutsRoot);
        var packageManager = new FrontedLayoutPackageManager(
            Path.Combine(root, "packages"),
            builtInLayoutsRoot,
            root,
            NullLogger<FrontedLayoutPackageManager>.Instance);
        return new FrontedBehaviorService(
            new FrontedUserLayoutStore(root),
            packageManager,
            NullLogger<FrontedBehaviorService>.Instance);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "neo-bpsys-behavior-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
