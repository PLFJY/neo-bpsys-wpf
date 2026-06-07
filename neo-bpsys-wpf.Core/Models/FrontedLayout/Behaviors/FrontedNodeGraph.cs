namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedNodeGraph
{
    public List<FrontedNode> Nodes { get; set; } = [];

    public List<FrontedNodeConnection> Connections { get; set; } = [];

    public FrontedNode? FindNode(Guid nodeId) =>
        Nodes.FirstOrDefault(node => node.NodeId == nodeId);

    public bool RemoveNode(Guid nodeId)
    {
        var node = FindNode(nodeId);
        if (node is null)
        {
            return false;
        }

        Nodes.Remove(node);
        RemoveConnectionsForNode(nodeId);
        return true;
    }

    public int RemoveConnectionsForNode(Guid nodeId) =>
        Connections.RemoveAll(connection =>
            connection.SourceNodeId == nodeId || connection.TargetNodeId == nodeId);

    public IReadOnlyList<FrontedNodeConnection> GetOutgoing(Guid nodeId, string? port = null) =>
        Connections
            .Where(connection => connection.SourceNodeId == nodeId
                                 && (port is null || connection.SourcePort == port))
            .ToArray();

    public IReadOnlyList<FrontedNodeConnection> GetIncoming(Guid nodeId, string? port = null) =>
        Connections
            .Where(connection => connection.TargetNodeId == nodeId
                                 && (port is null || connection.TargetPort == port))
            .ToArray();
}
