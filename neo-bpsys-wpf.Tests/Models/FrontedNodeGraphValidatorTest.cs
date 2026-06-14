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
    public void Validator_MultipleEnds_Error()
    {
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.end"), _catalog.CreateNode("flow.end")] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "MultipleEnds" && message.Severity == FrontedNodeGraphValidationSeverity.Error);
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

    [Fact]
    public void Validate_AnimateOpacityRejectsOutOfRange()
    {
        var node = _catalog.CreateNode("action.animateProperty");
        node.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        node.Properties["To"] = JsonSerializer.SerializeToElement("2");
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), node] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "InvalidNumericValue" && message.PropertyName == "To");
    }

    [Fact]
    public void Validate_AnimateFillColorAcceptsNamedColor()
    {
        var node = _catalog.CreateNode("action.animateProperty");
        node.Properties["PropertyName"] = JsonSerializer.SerializeToElement("FillColor");
        node.Properties["To"] = JsonSerializer.SerializeToElement("White");
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), node] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.DoesNotContain(messages, message => message.Code == "InvalidColorValue");
    }

    [Fact]
    public void Validate_VisibilityRejectsInvalidValue()
    {
        var node = _catalog.CreateNode("action.setProperty");
        node.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Visibility");
        node.Properties["Value"] = JsonSerializer.SerializeToElement("Shown");
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), node] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "InvalidVisibilityValue");
    }

    [Fact]
    public void Validate_RotationRejectsNaN()
    {
        var node = _catalog.CreateNode("action.animateProperty");
        node.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Rotation");
        node.Properties["To"] = JsonSerializer.SerializeToElement("NaN");
        var graph = new FrontedNodeGraph { Nodes = [_catalog.CreateNode("flow.start"), node] };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "InvalidNumericValue");
    }

    [Fact]
    public void Validate_ParallelBranchesWithoutOut_WarnsButAllowsEndAsOut()
    {
        var start = _catalog.CreateNode("flow.start");
        var parallel = _catalog.CreateNode("flow.parallel");
        var branch = _catalog.CreateNode("action.log");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, parallel, branch, end],
            Connections =
            [
                Link(start, "Out", parallel, "In"),
                Link(parallel, "Branch1", branch, "In")
            ]
        };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "ParallelBranchesNoOut" && message.Severity == FrontedNodeGraphValidationSeverity.Warning);

        graph.Connections.Add(Link(parallel, "Out", end, "In"));
        messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.DoesNotContain(messages, message => message.Code == "ParallelBranchesNoOut");
    }

    [Fact]
    public void Validate_ParallelBranchCountAboveTwenty_IsInvalid()
    {
        var parallel = _catalog.CreateNode("flow.parallel");
        parallel.Properties["BranchCount"] = JsonSerializer.SerializeToElement(21);

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(new FrontedNodeGraph { Nodes = [parallel] });

        Assert.Contains(messages, message => message.Code == "InvalidParallelBranchCount");
    }

    [Fact]
    public void Validate_EndInput_AllowsMultipleIncomingConnections()
    {
        var start = _catalog.CreateNode("flow.start");
        var condition = _catalog.CreateNode("flow.if");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, condition, end],
            Connections =
            [
                Link(start, "Out", condition, "In"),
                Link(condition, "True", end, "In"),
                Link(condition, "False", end, "In")
            ]
        };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.DoesNotContain(messages, message => message.Code == "InputMultipleConnections");
    }

    [Fact]
    public void Validate_NonEndInput_RejectsMultipleIncomingConnections()
    {
        var start = _catalog.CreateNode("flow.start");
        var condition = _catalog.CreateNode("flow.if");
        var log = _catalog.CreateNode("action.log");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, condition, log],
            Connections =
            [
                Link(start, "Out", condition, "In"),
                Link(condition, "True", log, "In"),
                Link(condition, "False", log, "In")
            ]
        };

        var messages = new FrontedNodeGraphValidator(_catalog).Validate(graph);

        Assert.Contains(messages, message => message.Code == "InputMultipleConnections");
    }

    private static FrontedNodeConnection Link(FrontedNode source, string sourcePort, FrontedNode target, string targetPort) =>
        new()
        {
            SourceNodeId = source.NodeId,
            SourcePort = sourcePort,
            TargetNodeId = target.NodeId,
            TargetPort = targetPort
        };
}
