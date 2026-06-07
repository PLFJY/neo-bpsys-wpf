using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Text.Json;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedNodeGraphValidatorTest
{
    private readonly FrontedNodeCatalog _catalog = new();

    [Fact]
    public void Validator_MissingStart_Warning()
    {
        var messages = new FrontedNodeGraphValidator(_catalog).Validate(new FrontedNodeGraph());

        Assert.Contains(messages, message => message.Code == "MissingStart" && message.Severity == FrontedNodeGraphValidationSeverity.Warning);
    }

    [Fact]
    public void Validator_MultipleStarts_WarningOrError()
    {
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), _catalog.CreateNode("flow.start")] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "MultipleStarts" && message.Severity == FrontedNodeGraphValidationSeverity.Error);
    }

    [Fact]
    public void Validator_MissingTargetNode_Error()
    {
        var start = _catalog.CreateNode("flow.start");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start],
            Connections = [new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Out", TargetNodeId = Guid.NewGuid(), TargetPort = "In" }]
        };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "MissingTargetNode");
    }

    [Fact]
    public void Validator_InvalidPort_Error()
    {
        var start = _catalog.CreateNode("flow.start");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, end],
            Connections = [new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Bad", TargetNodeId = end.NodeId, TargetPort = "In" }]
        };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "InvalidSourcePort");
    }

    [Fact]
    public void Validator_DelayNegativeDuration_Error()
    {
        var delay = _catalog.CreateNode("flow.delay");
        delay.Properties["DurationMs"] = JsonSerializer.SerializeToElement(-1);
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), delay] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "InvalidDuration");
    }

    [Fact]
    public void Validator_AnimatePropertyMissingPropertyName_WarningOrError()
    {
        var node = _catalog.CreateNode("action.animateProperty");
        node.Properties["PropertyName"] = JsonSerializer.SerializeToElement("");
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), node] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "RequiredPropertyMissing");
    }
}
