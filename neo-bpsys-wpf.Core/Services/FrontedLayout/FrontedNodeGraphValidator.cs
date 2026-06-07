using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedNodeGraphValidator(FrontedNodeCatalog? catalog = null)
{
    private readonly FrontedNodeCatalog _catalog = catalog ?? new FrontedNodeCatalog();

    public IReadOnlyList<FrontedNodeGraphValidationMessage> Validate(FrontedNodeGraph graph)
    {
        var messages = new List<FrontedNodeGraphValidationMessage>();
        var starts = graph.Nodes.Count(node => node.NodeType == "flow.start");
        if (starts == 0)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Warning, "MissingStart", "Graph has no Start node."));
        }
        else if (starts > 1)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "MultipleStarts", "Graph has multiple Start nodes."));
        }

        foreach (var node in graph.Nodes)
        {
            ValidateNode(node, messages);
        }

        foreach (var connection in graph.Connections)
        {
            ValidateConnection(graph, connection, messages);
        }

        foreach (var duplicate in graph.Connections.GroupBy(connection =>
                     (connection.SourceNodeId, connection.SourcePort, connection.TargetNodeId, connection.TargetPort))
                 .Where(group => group.Count() > 1))
        {
            foreach (var connection in duplicate.Skip(1))
            {
                messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "DuplicateConnection", "Duplicate connection.", connectionId: connection.ConnectionId));
            }
        }

        foreach (var port in graph.Connections.GroupBy(connection => (connection.SourceNodeId, connection.SourcePort)).Where(group => group.Count() > 1))
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "FlowOutputMultipleConnections", "A flow output port can only have one connection.", nodeId: port.Key.SourceNodeId));
        }

        foreach (var port in graph.Connections.GroupBy(connection => (connection.TargetNodeId, connection.TargetPort)).Where(group => group.Count() > 1))
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "InputMultipleConnections", "An input port can only have one connection.", nodeId: port.Key.TargetNodeId));
        }

        return messages;
    }

    private void ValidateNode(FrontedNode node, ICollection<FrontedNodeGraphValidationMessage> messages)
    {
        var descriptor = _catalog.Find(node.NodeType);
        if (descriptor is null)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Warning, "UnknownNodeType", $"Unknown node type '{node.NodeType}'.", node.NodeId));
            return;
        }

        foreach (var property in descriptor.Properties.Where(property => property.IsRequired))
        {
            if (!node.Properties.TryGetValue(property.Name, out var value) || IsEmpty(value))
            {
                messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "RequiredPropertyMissing", $"Required property '{property.Name}' is missing.", node.NodeId, propertyName: property.Name));
            }
        }

        if (node.NodeType is "flow.delay" or "action.animateProperty"
            && (!TryGetInt(node, "DurationMs", out var duration) || duration < 0))
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "InvalidDuration", "DurationMs must be a non-negative number.", node.NodeId, propertyName: "DurationMs"));
        }

        if (node.NodeType == "flow.if"
            && (!node.Properties.TryGetValue("Operator", out var operatorValue)
                || operatorValue.ValueKind != JsonValueKind.String
                || !Enum.TryParse<TriggerFilterOperator>(operatorValue.GetString(), out _)))
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "InvalidIfOperator", "If operator is invalid.", node.NodeId, propertyName: "Operator"));
        }
    }

    private void ValidateConnection(FrontedNodeGraph graph, FrontedNodeConnection connection, ICollection<FrontedNodeGraphValidationMessage> messages)
    {
        var source = graph.FindNode(connection.SourceNodeId);
        var target = graph.FindNode(connection.TargetNodeId);
        if (source is null)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "MissingSourceNode", "Connection source node is missing.", connectionId: connection.ConnectionId));
            return;
        }

        if (target is null)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "MissingTargetNode", "Connection target node is missing.", connectionId: connection.ConnectionId));
            return;
        }

        var sourceDescriptor = _catalog.Find(source.NodeType);
        var targetDescriptor = _catalog.Find(target.NodeType);
        if (sourceDescriptor is null || targetDescriptor is null)
        {
            return;
        }

        var sourcePort = sourceDescriptor.OutputPorts.FirstOrDefault(port => port.Name == connection.SourcePort);
        var targetPort = targetDescriptor.InputPorts.FirstOrDefault(port => port.Name == connection.TargetPort);
        if (sourcePort is null)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "InvalidSourcePort", "Connection source port is invalid.", source.NodeId, connection.ConnectionId));
            return;
        }

        if (targetPort is null)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "InvalidTargetPort", "Connection target port is invalid.", target.NodeId, connection.ConnectionId));
            return;
        }

        var compatible = sourcePort.PortKind == FrontedNodePortKind.FlowOut && targetPort.PortKind == FrontedNodePortKind.FlowIn
                         || sourcePort.PortKind == FrontedNodePortKind.ValueOut && targetPort.PortKind == FrontedNodePortKind.ValueIn;
        if (!compatible)
        {
            messages.Add(Message(FrontedNodeGraphValidationSeverity.Error, "IncompatiblePorts", "Connection ports are incompatible.", connectionId: connection.ConnectionId));
        }
    }

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString());

    private static bool TryGetInt(FrontedNode node, string propertyName, out int value)
    {
        value = 0;
        return node.Properties.TryGetValue(propertyName, out var element)
               && (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)
                   || element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value));
    }

    private static FrontedNodeGraphValidationMessage Message(
        FrontedNodeGraphValidationSeverity severity,
        string code,
        string message,
        Guid? nodeId = null,
        Guid? connectionId = null,
        string? propertyName = null) =>
        new() { Severity = severity, Code = code, Message = message, NodeId = nodeId, ConnectionId = connectionId, PropertyName = propertyName };
}
