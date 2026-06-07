using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedNodeGraphRuntimeTest
{
    private readonly FrontedNodeCatalog _catalog = new();

    [Fact]
    public async Task Runtime_StartLogEnd_ProducesLog()
    {
        var graph = Connect(_catalog.CreateNode("flow.start"), _catalog.CreateNode("action.log"), _catalog.CreateNode("flow.end"));
        graph.Nodes.Single(node => node.NodeType == "action.log").Properties["Message"] = JsonSerializer.SerializeToElement("hello");

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Contains(result.LogItems, item => item.Message == "hello");
    }

    [Fact]
    public async Task Runtime_Delay_WaitsApproximatelyOrUsesFakeClock()
    {
        var delayProvider = new FakeDelayProvider();
        var graph = Connect(_catalog.CreateNode("flow.start"), _catalog.CreateNode("flow.delay"), _catalog.CreateNode("flow.end"));

        await CreateRuntime(delayProvider).ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMilliseconds(300), Assert.Single(delayProvider.Delays));
    }

    [Fact]
    public async Task Runtime_IfTrue_TakesTrueBranch()
    {
        var graph = IfGraph("yes");

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext
        {
            EventPayload = new Dictionary<string, object?> { ["Value"] = "yes" }
        }, TestContext.Current.CancellationToken);

        Assert.Contains(result.LogItems, item => item.Message == "true");
        Assert.DoesNotContain(result.LogItems, item => item.Message == "false");
    }

    [Fact]
    public async Task Runtime_IfFalse_TakesFalseBranch()
    {
        var graph = IfGraph("no");

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext
        {
            EventPayload = new Dictionary<string, object?> { ["Value"] = "yes" }
        }, TestContext.Current.CancellationToken);

        Assert.Contains(result.LogItems, item => item.Message == "false");
    }

    [Fact]
    public async Task Runtime_Parallel_ExecutesBothBranches()
    {
        var start = _catalog.CreateNode("flow.start");
        var parallel = _catalog.CreateNode("flow.parallel");
        var first = LogNode("one");
        var second = LogNode("two");
        var graph = new FrontedNodeGraph { Nodes = [start, parallel, first, second] };
        graph.Connections.Add(Link(start, "Out", parallel, "In"));
        graph.Connections.Add(Link(parallel, "Branch1", first, "In"));
        graph.Connections.Add(Link(parallel, "Branch2", second, "In"));

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Contains(result.LogItems, item => item.Message == "one");
        Assert.Contains(result.LogItems, item => item.Message == "two");
    }

    [Fact]
    public async Task Runtime_SetProperty_EmitsActionRequest()
    {
        var graph = Connect(_catalog.CreateNode("flow.start"), _catalog.CreateNode("action.setProperty"), _catalog.CreateNode("flow.end"));
        graph.Nodes.Single(node => node.NodeType == "action.setProperty").Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphActionRequestType.SetProperty, Assert.Single(result.ActionRequests).RequestType);
    }

    [Fact]
    public async Task Runtime_AnimateProperty_EmitsActionRequest()
    {
        var graph = Connect(_catalog.CreateNode("flow.start"), _catalog.CreateNode("action.animateProperty"), _catalog.CreateNode("flow.end"));
        graph.Nodes.Single(node => node.NodeType == "action.animateProperty").Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphActionRequestType.AnimateProperty, Assert.Single(result.ActionRequests).RequestType);
    }

    [Fact]
    public async Task GraphRuntime_ActionExecutor_IsCalledWhenActionNodeExecutes()
    {
        var graph = Connect(_catalog.CreateNode("flow.start"), _catalog.CreateNode("action.setProperty"), _catalog.CreateNode("flow.end"));
        graph.Nodes.Single(node => node.NodeType == "action.setProperty").Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        var executor = new RecordingActionExecutor();

        var result = await CreateRuntime().ExecuteAsync(
            graph,
            new FrontedGraphExecutionContext { ActionExecutor = executor },
            TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Single(executor.Requests);
        Assert.Single(result.ActionRequests);
    }

    [Fact]
    public async Task GraphRuntime_ActionExecutor_PreservesSequenceDelayOrder()
    {
        var order = new List<string>();
        var delayProvider = new FakeDelayProvider { OnDelay = () => order.Add("delay") };
        var start = _catalog.CreateNode("flow.start");
        var sequence = _catalog.CreateNode("flow.sequence");
        var set = _catalog.CreateNode("action.setProperty");
        set.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        var delay = _catalog.CreateNode("flow.delay");
        var animate = _catalog.CreateNode("action.animateProperty");
        animate.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        var graph = new FrontedNodeGraph { Nodes = [start, sequence, set, delay, animate] };
        graph.Connections.Add(Link(start, "Out", sequence, "In"));
        graph.Connections.Add(Link(sequence, "Step1", set, "In"));
        graph.Connections.Add(Link(sequence, "Step2", delay, "In"));
        graph.Connections.Add(Link(delay, "Out", animate, "In"));
        var executor = new RecordingActionExecutor(request => order.Add(request.RequestType.ToString()));

        await CreateRuntime(delayProvider).ExecuteAsync(
            graph,
            new FrontedGraphExecutionContext { ActionExecutor = executor },
            TestContext.Current.CancellationToken);

        Assert.Equal(["SetProperty", "delay", "AnimateProperty"], order);
    }

    [Fact]
    public async Task Runtime_UnknownNode_LogsWarningAndDoesNotCrash()
    {
        var start = _catalog.CreateNode("flow.start");
        var unknown = new FrontedNode { NodeType = "plugin.missing" };
        var graph = new FrontedNodeGraph { Nodes = [start, unknown], Connections = [Link(start, "Out", unknown, "In")] };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Contains(result.LogItems, item => item.Level == FrontedGraphExecutionLogLevel.Warning);
    }

    [Fact]
    public async Task Runtime_Cancellation_CancelsDelay()
    {
        var delayProvider = new FakeDelayProvider { CancelInsideDelay = true };
        var graph = Connect(_catalog.CreateNode("flow.start"), _catalog.CreateNode("flow.delay"), _catalog.CreateNode("flow.end"));
        using var cts = new CancellationTokenSource();

        var result = await CreateRuntime(delayProvider).ExecuteAsync(graph, new FrontedGraphExecutionContext(), cts.Token);

        Assert.Equal(FrontedGraphExecutionStatus.Cancelled, result.Status);
    }

    private FrontedNodeGraphRuntime CreateRuntime(IFrontedGraphDelayProvider? delayProvider = null) =>
        new(_catalog, new FrontedNodeGraphValidator(_catalog), delayProvider);

    private FrontedNode LogNode(string message)
    {
        var node = _catalog.CreateNode("action.log");
        node.Properties["Message"] = JsonSerializer.SerializeToElement(message);
        return node;
    }

    private FrontedNodeGraph IfGraph(string right)
    {
        var start = _catalog.CreateNode("flow.start");
        var ifNode = _catalog.CreateNode("flow.if");
        ifNode.Properties["Left"] = JsonSerializer.SerializeToElement("Event.Value");
        ifNode.Properties["Right"] = JsonSerializer.SerializeToElement(right);
        var trueLog = LogNode("true");
        var falseLog = LogNode("false");
        return new FrontedNodeGraph
        {
            Nodes = [start, ifNode, trueLog, falseLog],
            Connections =
            [
                Link(start, "Out", ifNode, "In"),
                Link(ifNode, "True", trueLog, "In"),
                Link(ifNode, "False", falseLog, "In")
            ]
        };
    }

    private static FrontedNodeGraph Connect(FrontedNode first, FrontedNode second, FrontedNode third) =>
        new()
        {
            Nodes = [first, second, third],
            Connections = [Link(first, "Out", second, "In"), Link(second, "Out", third, "In")]
        };

    private static FrontedNodeConnection Link(FrontedNode source, string sourcePort, FrontedNode target, string targetPort) =>
        new() { SourceNodeId = source.NodeId, SourcePort = sourcePort, TargetNodeId = target.NodeId, TargetPort = targetPort };

    private sealed class FakeDelayProvider : IFrontedGraphDelayProvider
    {
        public List<TimeSpan> Delays { get; } = [];
        public bool CancelInsideDelay { get; init; }
        public Action? OnDelay { get; init; }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            OnDelay?.Invoke();
            if (CancelInsideDelay)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingActionExecutor(Action<FrontedGraphActionRequest>? onExecute = null) : IFrontedGraphActionExecutor
    {
        public List<FrontedGraphActionRequest> Requests { get; } = [];

        public Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            onExecute?.Invoke(request);
            return Task.CompletedTask;
        }
    }
}
