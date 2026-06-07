#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedBehaviorModelTest
{
    [Fact]
    public void FrontedBehaviorDocument_Defaults_AreSane()
    {
        var document = new FrontedBehaviorDocument();

        Assert.Equal(1, document.Version);
        Assert.NotNull(document.ControlBehaviorSets);
        Assert.Empty(document.ControlBehaviorSets);
    }

    [Fact]
    public void ControlBehaviorSet_LookupByGuid_ReturnsCorrectSet()
    {
        var expectedGuid = Guid.NewGuid();
        var otherGuid = Guid.NewGuid();
        var document = new FrontedBehaviorDocument
        {
            ControlBehaviorSets =
            [
                new ControlBehaviorSet { BehaviorGuid = otherGuid, DisplayName = "Other" },
                new ControlBehaviorSet { BehaviorGuid = expectedGuid, DisplayName = "Expected" }
            ]
        };

        var set = document.FindSet(expectedGuid);

        Assert.NotNull(set);
        Assert.Equal("Expected", set.DisplayName);
    }

    [Fact]
    public void ControlBehaviorSet_GetOrCreateByGuid_ReusesExistingSet()
    {
        var behaviorGuid = Guid.NewGuid();
        var document = new FrontedBehaviorDocument();

        var created = document.GetOrCreateSet(behaviorGuid, "Title");
        var existing = document.GetOrCreateSet(behaviorGuid, "Ignored");

        Assert.Same(created, existing);
        Assert.Single(document.ControlBehaviorSets);
        Assert.Equal("Title", existing.DisplayName);
    }

    [Fact]
    public void ControlBehaviorSet_RemoveByGuid_RemovesOnlyExpectedSet()
    {
        var removedGuid = Guid.NewGuid();
        var keptGuid = Guid.NewGuid();
        var document = new FrontedBehaviorDocument
        {
            ControlBehaviorSets =
            [
                new ControlBehaviorSet { BehaviorGuid = removedGuid, DisplayName = "Removed" },
                new ControlBehaviorSet { BehaviorGuid = keptGuid, DisplayName = "Kept" }
            ]
        };

        var removed = document.RemoveSet(removedGuid);

        Assert.True(removed);
        Assert.Null(document.FindSet(removedGuid));
        Assert.NotNull(document.FindSet(keptGuid));
    }

    [Fact]
    public void FrontedBehavior_OneShotDefaults_AreSane()
    {
        var behavior = new FrontedBehavior();

        Assert.NotEqual(Guid.Empty, behavior.BehaviorId);
        Assert.Equal(string.Empty, behavior.Name);
        Assert.True(behavior.Enabled);
        Assert.Equal(FrontedBehaviorKind.OneShot, behavior.Kind);
        Assert.NotNull(behavior.Graph);
    }

    [Fact]
    public void FrontedBehavior_LoopDefaults_AreSane()
    {
        var behavior = new FrontedBehavior { Kind = FrontedBehaviorKind.Loop };

        Assert.Equal(FrontedBehaviorKind.Loop, behavior.Kind);
        Assert.NotNull(behavior.StartGraph);
        Assert.NotNull(behavior.LoopGraph);
        Assert.NotNull(behavior.StopGraph);
        Assert.NotNull(behavior.LoopPolicy);
        Assert.Equal(FrontedReentryPolicy.IgnoreIfRunning, behavior.LoopPolicy.ReentryPolicy);
    }

    [Fact]
    public void FrontedNodeGraph_EmptyGraph_IsSerializable()
    {
        var roundTrip = JsonSerializer.Deserialize<FrontedNodeGraph>(
            JsonSerializer.Serialize(new FrontedNodeGraph()));

        Assert.NotNull(roundTrip);
        Assert.NotNull(roundTrip.Nodes);
        Assert.NotNull(roundTrip.Connections);
        Assert.Empty(roundTrip.Nodes);
        Assert.Empty(roundTrip.Connections);
    }

    [Fact]
    public void FrontedNodeGraph_NodeAndConnection_RoundTrip()
    {
        var sourceNodeId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var graph = new FrontedNodeGraph
        {
            Nodes =
            [
                new FrontedNode
                {
                    NodeId = sourceNodeId,
                    NodeType = "event.start",
                    DisplayName = "Start",
                    X = 10,
                    Y = 20
                },
                new FrontedNode
                {
                    NodeId = targetNodeId,
                    NodeType = "animation.fade",
                    DisplayName = "Fade",
                    X = 30,
                    Y = 40,
                    Properties =
                    {
                        ["DurationMs"] = JsonSerializer.SerializeToElement(300)
                    }
                }
            ],
            Connections =
            [
                new FrontedNodeConnection
                {
                    ConnectionId = connectionId,
                    SourceNodeId = sourceNodeId,
                    SourcePort = "Out",
                    TargetNodeId = targetNodeId,
                    TargetPort = "In"
                }
            ]
        };

        var roundTrip = JsonSerializer.Deserialize<FrontedNodeGraph>(JsonSerializer.Serialize(graph));

        Assert.NotNull(roundTrip);
        Assert.Equal(new[] { sourceNodeId, targetNodeId }, roundTrip.Nodes.Select(node => node.NodeId).ToArray());
        var connection = Assert.Single(roundTrip.Connections);
        Assert.Equal(connectionId, connection.ConnectionId);
        Assert.Equal(sourceNodeId, connection.SourceNodeId);
        Assert.Equal("Out", connection.SourcePort);
        Assert.Equal(targetNodeId, connection.TargetNodeId);
        Assert.Equal("In", connection.TargetPort);
        Assert.Equal(300, roundTrip.Nodes[1].Properties["DurationMs"].GetInt32());
    }
}
