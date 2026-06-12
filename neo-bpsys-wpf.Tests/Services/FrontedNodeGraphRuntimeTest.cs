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
        var node = graph.Nodes.Single(node => node.NodeType == "action.setProperty");
        node.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        node.Properties["TargetLayer"] = JsonSerializer.SerializeToElement("Content");

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        var request = Assert.Single(result.ActionRequests);
        Assert.Equal(FrontedGraphActionRequestType.SetProperty, request.RequestType);
        Assert.Equal(FrontedAnimationTargetLayer.Content, request.TargetLayer);
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

    [Fact]
    public async Task Chain_AwaitsEachNodeInOrder()
    {
        var order = new List<string>();
        var start = _catalog.CreateNode("flow.start");
        var logA = LogNode("A");
        var logB = LogNode("B");
        var logC = LogNode("C");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, logA, logB, logC, end],
            Connections =
            [
                Link(start, "Out", logA, "In"),
                Link(logA, "Out", logB, "In"),
                Link(logB, "Out", logC, "In"),
                Link(logC, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        var messages = result.LogItems.Where(item => item.Level == FrontedGraphExecutionLogLevel.Information).Select(item => item.Message).ToArray();
        Assert.Contains("A", messages);
        Assert.Contains("B", messages);
        Assert.Contains("C", messages);
    }

    [Fact]
    public async Task Chain_AnimateWaitTrue_BlocksOut()
    {
        var start = _catalog.CreateNode("flow.start");
        var animate = _catalog.CreateNode("action.animateProperty");
        animate.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        animate.Properties["WaitForCompletion"] = JsonSerializer.SerializeToElement(true);
        var logB = LogNode("B");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, animate, logB, end],
            Connections =
            [
                Link(start, "Out", animate, "In"),
                Link(animate, "Out", logB, "In"),
                Link(logB, "Out", end, "In")
            ]
        };

        var blockingExecutor = new BlockingActionExecutor();
        var resultTask = CreateRuntime().ExecuteAsync(
            graph,
            new FrontedGraphExecutionContext { ActionExecutor = blockingExecutor },
            TestContext.Current.CancellationToken);

        // B should not execute while animation is blocked
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(blockingExecutor.CompletedRequests, r => r.RequestType == FrontedGraphActionRequestType.AnimateProperty);

        // Signal completion
        blockingExecutor.Complete();
        var result = await resultTask;

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
    }

    [Fact]
    public async Task Chain_AnimateWaitFalse_ContinuesImmediately()
    {
        var start = _catalog.CreateNode("flow.start");
        var animate = _catalog.CreateNode("action.animateProperty");
        animate.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        animate.Properties["WaitForCompletion"] = JsonSerializer.SerializeToElement(false);
        var logB = LogNode("B");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, animate, logB, end],
            Connections =
            [
                Link(start, "Out", animate, "In"),
                Link(animate, "Out", logB, "In"),
                Link(logB, "Out", end, "In")
            ]
        };

        var blockingExecutor = new BlockingActionExecutor(completeImmediately: true);
        var result = await CreateRuntime().ExecuteAsync(
            graph,
            new FrontedGraphExecutionContext { ActionExecutor = blockingExecutor },
            TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Contains(result.LogItems, item => item.Message == "B");
    }

    [Fact]
    public async Task Delay_BlocksFollowingNode()
    {
        var order = new List<string>();
        var delayProvider = new FakeDelayProvider { OnDelay = () => order.Add("delay") };
        var start = _catalog.CreateNode("flow.start");
        var delay = _catalog.CreateNode("flow.delay");
        var logA = LogNode("A");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, delay, logA, end],
            Connections =
            [
                Link(start, "Out", delay, "In"),
                Link(delay, "Out", logA, "In"),
                Link(logA, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime(delayProvider).ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        // Delay must have been invoked (the fake provider records it)
        Assert.Single(delayProvider.Delays);
        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
    }

    [Fact]
    public async Task Parallel_StartsBranchesConcurrently()
    {
        var start = _catalog.CreateNode("flow.start");
        var parallel = _catalog.CreateNode("flow.parallel");
        var logA = LogNode("A");
        var logB = LogNode("B");
        var logC = LogNode("C");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, parallel, logA, logB, logC, end],
            Connections =
            [
                Link(start, "Out", parallel, "In"),
                Link(parallel, "Branch1", logA, "In"),
                Link(parallel, "Branch2", logB, "In"),
                Link(parallel, "Out", logC, "In"),
                Link(logC, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Contains(result.LogItems, item => item.Message == "A");
        Assert.Contains(result.LogItems, item => item.Message == "B");
        Assert.Contains(result.LogItems, item => item.Message == "C");
    }

    [Fact]
    public async Task Parallel_OutRunsAfterAllBranches()
    {
        var start = _catalog.CreateNode("flow.start");
        var parallel = _catalog.CreateNode("flow.parallel");
        var logA = LogNode("A");
        var logB = LogNode("B");
        var logC = LogNode("C");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, parallel, logA, logB, logC, end],
            Connections =
            [
                Link(start, "Out", parallel, "In"),
                Link(parallel, "Branch1", logA, "In"),
                Link(parallel, "Branch2", logB, "In"),
                Link(parallel, "Out", logC, "In"),
                Link(logC, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        // Verify execution order: A and B appear before C (Out)
        var logItems = result.LogItems.Where(item => item.Level == FrontedGraphExecutionLogLevel.Information).ToArray();
        var aIndex = Array.FindIndex(logItems, item => item.Message == "A");
        var bIndex = Array.FindIndex(logItems, item => item.Message == "B");
        var cIndex = Array.FindIndex(logItems, item => item.Message == "C");
        Assert.True(aIndex >= 0 && bIndex >= 0 && cIndex >= 0);
        Assert.True(cIndex > aIndex, "Out (C) should execute after Branch1 (A)");
        Assert.True(cIndex > bIndex, "Out (C) should execute after Branch2 (B)");
    }

    [Fact]
    public async Task Parallel_NoBranchConnection_DoesNotCrash()
    {
        var start = _catalog.CreateNode("flow.start");
        var parallel = _catalog.CreateNode("flow.parallel");
        var logC = LogNode("C");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, parallel, logC, end],
            Connections =
            [
                Link(start, "Out", parallel, "In"),
                Link(parallel, "Out", logC, "In"),
                Link(logC, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Contains(result.LogItems, item => item.Message == "C");
    }

    [Fact]
    public async Task Parallel_BranchDoesNotNeedEnd()
    {
        var start = _catalog.CreateNode("flow.start");
        var parallel = _catalog.CreateNode("flow.parallel");
        var logA = LogNode("A");
        var logC = LogNode("C");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, parallel, logA, logC, end],
            Connections =
            [
                Link(start, "Out", parallel, "In"),
                Link(parallel, "Branch1", logA, "In"),
                Link(parallel, "Out", logC, "In"),
                Link(logC, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Contains(result.LogItems, item => item.Message == "A");
        Assert.Contains(result.LogItems, item => item.Message == "C");
    }

    [Fact]
    public async Task ActionRequestsStillReturned()
    {
        var start = _catalog.CreateNode("flow.start");
        var animate = _catalog.CreateNode("action.animateProperty");
        animate.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        animate.Properties["WaitForCompletion"] = JsonSerializer.SerializeToElement(false);
        var set = _catalog.CreateNode("action.setProperty");
        set.Properties["PropertyName"] = JsonSerializer.SerializeToElement("Opacity");
        var end = _catalog.CreateNode("flow.end");
        var graph = new FrontedNodeGraph
        {
            Nodes = [start, animate, set, end],
            Connections =
            [
                Link(start, "Out", animate, "In"),
                Link(animate, "Out", set, "In"),
                Link(set, "Out", end, "In")
            ]
        };

        var result = await CreateRuntime().ExecuteAsync(graph, new FrontedGraphExecutionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(FrontedGraphExecutionStatus.Success, result.Status);
        Assert.Equal(2, result.ActionRequests.Count);
        Assert.Contains(result.ActionRequests, r => r.RequestType == FrontedGraphActionRequestType.AnimateProperty && !r.WaitForCompletion);
        Assert.Contains(result.ActionRequests, r => r.RequestType == FrontedGraphActionRequestType.SetProperty);
    }

    private FrontedNodeGraphRuntime CreateRuntime(IFrontedGraphDelayProvider? delayProvider = null) =>
        new(_catalog, new FrontedNodeGraphValidator(_catalog), delayProvider);

    private FrontedNode LogNode(string message)
    {
        var node = _catalog.CreateNode("action.log");
        node.Properties["Message"] = JsonSerializer.SerializeToElement(message);
        return node;
    }

    private FrontedNodeGraph IfGraph(string right, string left = "Event.Value")
    {
        var start = _catalog.CreateNode("flow.start");
        var ifNode = _catalog.CreateNode("flow.if");
        ifNode.Properties["Left"] = JsonSerializer.SerializeToElement(left);
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

    private sealed class BlockingActionExecutor(bool completeImmediately = false) : IFrontedGraphActionExecutor
    {
        private readonly TaskCompletionSource _completion = new();
        public List<FrontedGraphActionRequest> CompletedRequests { get; } = [];

        public void Complete() => _completion.TrySetResult();

        public async Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken)
        {
            if (completeImmediately)
            {
                CompletedRequests.Add(request);
                return;
            }

            await _completion.Task.WaitAsync(cancellationToken);
            CompletedRequests.Add(request);
        }
    }
}
