using System.Collections.Concurrent;
using System.Text.Json;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedNodeGraphRuntime(
    FrontedNodeCatalog? catalog = null,
    FrontedNodeGraphValidator? validator = null,
    IFrontedGraphDelayProvider? delayProvider = null) : IFrontedNodeGraphRuntime
{
    private const int MaxNodeExecutions = 1000;
    private readonly FrontedNodeCatalog _catalog = catalog ?? new FrontedNodeCatalog();
    private readonly FrontedNodeGraphValidator _validator = validator ?? new FrontedNodeGraphValidator(catalog);
    private readonly IFrontedGraphDelayProvider _delayProvider = delayProvider ?? new FrontedGraphDelayProvider();

    public async Task<FrontedGraphExecutionResult> ExecuteAsync(
        FrontedNodeGraph graph,
        FrontedGraphExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var logs = new ConcurrentQueue<FrontedGraphExecutionLogItem>();
        var actions = new ConcurrentQueue<FrontedGraphActionRequest>();
        var validation = _validator.Validate(graph);
        var startNodes = graph.Nodes.Where(node => node.NodeType == "flow.start").ToArray();
        if (startNodes.Length != 1 || validation.Any(message => message.Severity == FrontedNodeGraphValidationSeverity.Error))
        {
            foreach (var message in validation)
            {
                Log(logs, message.Severity == FrontedNodeGraphValidationSeverity.Error ? FrontedGraphExecutionLogLevel.Error : FrontedGraphExecutionLogLevel.Warning, message.Message, message.NodeId);
            }

            return Result(FrontedGraphExecutionStatus.Failed, logs, actions);
        }

        var state = new ExecutionState(graph, context, logs, actions, cancellationToken);
        try
        {
            Log(logs, FrontedGraphExecutionLogLevel.Information, "Graph preview started.");
            await ExecuteNodeAsync(startNodes[0], state);
            Log(logs, FrontedGraphExecutionLogLevel.Information, "Graph preview completed.");
            return Result(FrontedGraphExecutionStatus.Success, logs, actions);
        }
        catch (OperationCanceledException)
        {
            Log(logs, FrontedGraphExecutionLogLevel.Warning, "Graph preview cancelled.");
            return Result(FrontedGraphExecutionStatus.Cancelled, logs, actions);
        }
        catch (Exception exception)
        {
            Log(logs, FrontedGraphExecutionLogLevel.Error, exception.Message);
            return Result(FrontedGraphExecutionStatus.Failed, logs, actions, exception);
        }
    }

    private async Task ExecuteNodeAsync(FrontedNode node, ExecutionState state)
    {
        state.CancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref state.ExecutionCount) > MaxNodeExecutions)
        {
            throw new InvalidOperationException($"Graph exceeded the {MaxNodeExecutions} node execution limit.");
        }

        if (_catalog.Find(node.NodeType) is null)
        {
            Log(state.Logs, FrontedGraphExecutionLogLevel.Warning, $"Unknown node type '{node.NodeType}' skipped.", node.NodeId);
            return;
        }

        switch (node.NodeType)
        {
            case "flow.start":
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "flow.end":
                Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, "End reached.", node.NodeId);
                break;
            case "flow.delay":
                var duration = GetInt(node, "DurationMs");
                Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, $"Delay {duration} ms.", node.NodeId);
                await _delayProvider.DelayAsync(TimeSpan.FromMilliseconds(duration), state.CancellationToken);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "flow.sequence":
                foreach (var port in new[] { "Step1", "Step2", "Step3" })
                {
                    await ExecuteOutputAsync(node, port, state);
                }
                break;
            case "flow.parallel":
                await Task.WhenAll(new[] { "Branch1", "Branch2", "Branch3" }.Select(port => ExecuteOutputAsync(node, port, state)));
                break;
            case "flow.if":
                var left = ResolveText(GetString(node, "Left"), state.Context);
                var right = GetString(node, "Right");
                var op = Enum.TryParse<TriggerFilterOperator>(GetString(node, "Operator"), out var parsed) ? parsed : TriggerFilterOperator.Equals;
                var result = FrontedTriggerFilterTextComparer.Evaluate(left, op, right);
                Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, $"If evaluated to {result}.", node.NodeId);
                await ExecuteOutputAsync(node, result ? "True" : "False", state);
                break;
            case "action.log":
                Log(state.Logs, FrontedGraphExecutionLogLevel.Information, GetString(node, "Message"), node.NodeId);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "action.setProperty":
                EmitAction(node, FrontedGraphActionRequestType.SetProperty, state, ["Value"]);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "action.resetProperty":
                EmitAction(node, FrontedGraphActionRequestType.ResetProperty, state, []);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "action.animateProperty":
                EmitAction(node, FrontedGraphActionRequestType.AnimateProperty, state, ["From", "To", "Easing"], GetInt(node, "DurationMs"));
                await ExecuteOutputAsync(node, "Out", state);
                break;
            default:
                Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, $"Value node '{node.NodeType}' has no flow execution.", node.NodeId);
                break;
        }
    }

    private async Task ExecuteOutputAsync(FrontedNode node, string port, ExecutionState state)
    {
        var connection = state.Graph.GetOutgoing(node.NodeId, port).FirstOrDefault();
        var target = connection is null ? null : state.Graph.FindNode(connection.TargetNodeId);
        if (target is not null)
        {
            await ExecuteNodeAsync(target, state);
        }
    }

    private static void EmitAction(
        FrontedNode node,
        FrontedGraphActionRequestType requestType,
        ExecutionState state,
        IReadOnlyList<string> valueNames,
        int? durationMs = null)
    {
        var request = new FrontedGraphActionRequest
        {
            RequestType = requestType,
            Target = GetString(node, "Target", "Self"),
            PropertyName = GetString(node, "PropertyName"),
            Values = valueNames.ToDictionary(name => name, name => (string?)GetString(node, name)),
            DurationMs = durationMs
        };
        state.Actions.Enqueue(request);
        Log(state.Logs, FrontedGraphExecutionLogLevel.Information, $"{requestType}: {request.Target}.{request.PropertyName}", node.NodeId);
    }

    private static object? ResolveText(string value, FrontedGraphExecutionContext context)
    {
        if (value.StartsWith("Event.", StringComparison.Ordinal))
        {
            return context.EventPayload.GetValueOrDefault(value["Event.".Length..]);
        }

        if (value.StartsWith("SelfTag.", StringComparison.Ordinal))
        {
            return context.SelfTags.GetValueOrDefault(value["SelfTag.".Length..]);
        }

        return value;
    }

    private static string GetString(FrontedNode node, string name, string fallback = "")
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    private static int GetInt(FrontedNode node, string name)
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return 0;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : int.TryParse(value.ToString(), out number) ? number : 0;
    }

    private static void Log(ConcurrentQueue<FrontedGraphExecutionLogItem> logs, FrontedGraphExecutionLogLevel level, string message, Guid? nodeId = null) =>
        logs.Enqueue(new FrontedGraphExecutionLogItem { Level = level, Message = message, NodeId = nodeId });

    private static FrontedGraphExecutionResult Result(
        FrontedGraphExecutionStatus status,
        ConcurrentQueue<FrontedGraphExecutionLogItem> logs,
        ConcurrentQueue<FrontedGraphActionRequest> actions,
        Exception? exception = null) =>
        new() { Status = status, LogItems = logs.ToArray(), ActionRequests = actions.ToArray(), Exception = exception };

    private sealed class ExecutionState(
        FrontedNodeGraph graph,
        FrontedGraphExecutionContext context,
        ConcurrentQueue<FrontedGraphExecutionLogItem> logs,
        ConcurrentQueue<FrontedGraphActionRequest> actions,
        CancellationToken cancellationToken)
    {
        public FrontedNodeGraph Graph { get; } = graph;
        public FrontedGraphExecutionContext Context { get; } = context;
        public ConcurrentQueue<FrontedGraphExecutionLogItem> Logs { get; } = logs;
        public ConcurrentQueue<FrontedGraphActionRequest> Actions { get; } = actions;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public int ExecutionCount;
    }
}
