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

        Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, $"Node started: {node.NodeType}.", node.NodeId);
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
            case "flow.parallel":
                var branchPorts = FrontedParallelNodePorts.GetBranchPortNames(FrontedParallelNodePorts.GetBranchCount(node));
                var branchTasks = branchPorts
                    .Where(port => state.Graph.GetOutgoing(node.NodeId, port).Any())
                    .Select(port => ExecuteOutputAsync(node, port, state))
                    .ToArray();
                if (branchTasks.Length > 0)
                {
                    await Task.WhenAll(branchTasks);
                }
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "flow.if":
                var leftPath = GetString(node, "Left");
                var left = ResolveText(leftPath, state.Context, out var resolved);
                var right = GetString(node, "Right");
                var op = Enum.TryParse<TriggerFilterOperator>(GetString(node, "Operator"), out var parsed) ? parsed : TriggerFilterOperator.Equals;
                var result = FrontedTriggerFilterTextComparer.Evaluate(left, op, right);
                if (!resolved && IsPayloadPath(leftPath))
                {
                    Log(
                        state.Logs,
                        FrontedGraphExecutionLogLevel.Warning,
                        $"If condition unresolved: LeftPath={leftPath}; AvailableEventKeys={FormatAvailableKeys(state.Context)}",
                        node.NodeId);
                }
                Log(
                    state.Logs,
                    FrontedGraphExecutionLogLevel.Debug,
                    $"If condition: LeftPath={leftPath}; LeftValue={FrontedBehaviorPayloadValueFormatter.Format(left)}; Operator={op}; RightValue={right}; Result={result}",
                    node.NodeId);
                await ExecuteOutputAsync(node, result ? "True" : "False", state);
                break;
            case "action.log":
                Log(state.Logs, FrontedGraphExecutionLogLevel.Information, GetString(node, "Message"), node.NodeId);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "action.setProperty":
                await EmitActionAsync(node, FrontedGraphActionRequestType.SetProperty, state, ["Value"]);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "action.resetProperty":
                await EmitActionAsync(node, FrontedGraphActionRequestType.ResetProperty, state, []);
                await ExecuteOutputAsync(node, "Out", state);
                break;
            case "action.animateProperty":
                await EmitActionAsync(node, FrontedGraphActionRequestType.AnimateProperty, state, ["From", "To", "Easing"], GetInt(node, "DurationMs"), GetBool(node, "WaitForCompletion", true));
                await ExecuteOutputAsync(node, "Out", state);
                break;
            default:
                Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, $"Node '{node.NodeType}' has no flow execution behavior.", node.NodeId);
                break;
        }
        Log(state.Logs, FrontedGraphExecutionLogLevel.Debug, $"Node completed: {node.NodeType}.", node.NodeId);
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

    private static async Task EmitActionAsync(
        FrontedNode node,
        FrontedGraphActionRequestType requestType,
        ExecutionState state,
        IReadOnlyList<string> valueNames,
        int? durationMs = null,
        bool waitForCompletion = true)
    {
        var resolvedValues = new Dictionary<string, string?>();
        var numericResolver = new FrontedNumericGraphValueResolver(state.Graph, state.Context);
        foreach (var name in valueNames)
        {
            var portName = name switch { "Value" => "ValueInput", "From" => "FromInput", "To" => "ToInput", _ => string.Empty };
            var literal = GetString(node, name);
            var usesNumericValue = !string.IsNullOrEmpty(portName)
                                   && (literal.TrimStart().StartsWith('=')
                                       || state.Graph.Connections.Any(connection => connection.TargetNodeId == node.NodeId && connection.TargetPort == portName));
            if (usesNumericValue && !FrontedBehaviorPropertyMetadata.IsNumericProperty(GetString(node, "PropertyName")))
            {
                Log(state.Logs, FrontedGraphExecutionLogLevel.Warning, $"{requestType} skipped: numeric value '{name}' requires a numeric target property.", node.NodeId);
                return;
            }
            var resolvedValue = literal;
            if (!string.IsNullOrEmpty(portName) && !numericResolver.TryResolveActionValue(node, portName, literal, out resolvedValue, out var error))
            {
                Log(state.Logs, FrontedGraphExecutionLogLevel.Warning, $"{requestType} skipped: {name} could not be resolved: {error}", node.NodeId);
                return;
            }
            if (usesNumericValue && string.Equals(GetString(node, $"{name}InputUnit"), "Percent", StringComparison.OrdinalIgnoreCase))
            {
                if (!FrontedBehaviorPropertyMetadata.SupportsPercentage(GetString(node, "PropertyName")))
                {
                    Log(state.Logs, FrontedGraphExecutionLogLevel.Warning, $"{requestType} skipped: percent input '{name}' requires a relative-length target property.", node.NodeId);
                    return;
                }
                resolvedValue += "%";
            }
            resolvedValues[name] = string.IsNullOrEmpty(portName) ? literal : resolvedValue;
        }
        var request = new FrontedGraphActionRequest
        {
            RequestType = requestType,
            Target = GetString(node, "Target", "Self"),
            TargetLayer = GetTargetLayer(node),
            PropertyName = GetString(node, "PropertyName"),
            Values = resolvedValues,
            DurationMs = durationMs,
            WaitForCompletion = waitForCompletion
        };
        state.Actions.Enqueue(request);
        var values = string.Join(", ", request.Values.Select(pair => $"{pair.Key}={pair.Value}"));
        Log(
            state.Logs,
            FrontedGraphExecutionLogLevel.Information,
            $"{requestType}: {request.Target}[{request.TargetLayer}].{request.PropertyName}; {values}",
            node.NodeId);
        if (state.Context.ActionExecutor is not null)
        {
            await state.Context.ActionExecutor.ExecuteAsync(request, state.CancellationToken);
        }
    }

    private static FrontedAnimationTargetLayer GetTargetLayer(FrontedNode node) =>
        Enum.TryParse<FrontedAnimationTargetLayer>(
            GetString(node, "TargetLayer", FrontedAnimationTargetLayer.Auto.ToString()),
            true,
            out var layer)
            ? layer
            : FrontedAnimationTargetLayer.Auto;

    private static object? ResolveText(string value, FrontedGraphExecutionContext context, out bool resolved)
    {
        if (TryResolvePayloadPath(value, "Event.", context.EventPayload, ["Event."], out var eventValue))
        {
            resolved = true;
            return eventValue;
        }

        if (TryResolvePayloadPath(value, "StartEvent.", context.StartEventPayload, ["StartEvent.", "Event."], out var startEventValue))
        {
            resolved = true;
            return startEventValue;
        }

        if (TryResolvePayloadPath(value, "StopEvent.", context.StopEventPayload, ["StopEvent.", "Event."], out var stopEventValue))
        {
            resolved = true;
            return stopEventValue;
        }

        if (IsPayloadPath(value))
        {
            resolved = false;
            return null;
        }

        if (value.StartsWith("Context.", StringComparison.Ordinal))
        {
            var contextValue = value["Context.".Length..] switch
            {
                nameof(FrontedGraphExecutionContext.TriggerEventType) => (object?)context.TriggerEventType,
                nameof(FrontedGraphExecutionContext.CurrentControlDisplayName) => context.CurrentControlDisplayName,
                nameof(FrontedGraphExecutionContext.BehaviorGuid) => context.BehaviorGuid,
                _ => null
            };
            resolved = contextValue is not null;
            return contextValue;
        }

        resolved = true;
        return value;
    }

    private static bool TryResolvePayloadPath(
        string value,
        string prefix,
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyList<string> acceptedPrefixes,
        out object? resolved)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            resolved = null;
            return false;
        }

        var suffix = value[prefix.Length..];
        foreach (var key in new[] { suffix }.Concat(acceptedPrefixes.Select(candidate => candidate + suffix)))
        {
            if (payload.TryGetValue(key, out resolved))
            {
                return true;
            }
        }

        resolved = null;
        return false;
    }

    private static bool IsPayloadPath(string value) =>
        value.StartsWith("Event.", StringComparison.Ordinal)
        || value.StartsWith("StartEvent.", StringComparison.Ordinal)
        || value.StartsWith("StopEvent.", StringComparison.Ordinal);

    private static string FormatAvailableKeys(FrontedGraphExecutionContext context)
    {
        var keys = context.EventPayload.Keys
            .Concat(context.StartEventPayload.Keys)
            .Concat(context.StopEventPayload.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return keys.Length == 0 ? "(none)" : string.Join(", ", keys);
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

    private static bool GetBool(FrontedNode node, string name, bool fallback = false)
    {
        if (!node.Properties.TryGetValue(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var result) ? result : fallback;
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
