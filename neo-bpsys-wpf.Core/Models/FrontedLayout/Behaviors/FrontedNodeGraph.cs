namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 行为节点图，包含节点和连接。
/// </summary>
public sealed class FrontedNodeGraph
{
    /// <summary>
    /// 节点列表。
    /// </summary>
    public List<FrontedNode> Nodes { get; set; } = [];

    /// <summary>
    /// 连接列表。
    /// </summary>
    public List<FrontedNodeConnection> Connections { get; set; } = [];

    /// <summary>
    /// 根据节点 ID 查找节点。
    /// </summary>
    /// <param name="nodeId">节点 ID。</param>
    /// <returns>找到的节点，未找到时返回 null。</returns>
    public FrontedNode? FindNode(Guid nodeId) =>
        Nodes.FirstOrDefault(node => node.NodeId == nodeId);

    /// <summary>
    /// 移除指定节点及其连接。
    /// </summary>
    /// <param name="nodeId">节点 ID。</param>
    /// <returns>是否成功移除。</returns>
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

    /// <summary>
    /// 移除指定节点的所有连接。
    /// </summary>
    /// <param name="nodeId">节点 ID。</param>
    /// <returns>移除的连接数量。</returns>
    public int RemoveConnectionsForNode(Guid nodeId) =>
        Connections.RemoveAll(connection =>
            connection.SourceNodeId == nodeId || connection.TargetNodeId == nodeId);

    /// <summary>
    /// 获取节点的输出连接。
    /// </summary>
    /// <param name="nodeId">节点 ID。</param>
    /// <param name="port">端口名称（可选）。</param>
    /// <returns>输出连接列表。</returns>
    public IReadOnlyList<FrontedNodeConnection> GetOutgoing(Guid nodeId, string? port = null) =>
        Connections
            .Where(connection => connection.SourceNodeId == nodeId
                                 && (port is null || connection.SourcePort == port))
            .ToArray();

    /// <summary>
    /// 获取节点的输入连接。
    /// </summary>
    /// <param name="nodeId">节点 ID。</param>
    /// <param name="port">端口名称（可选）。</param>
    /// <returns>输入连接列表。</returns>
    public IReadOnlyList<FrontedNodeConnection> GetIncoming(Guid nodeId, string? port = null) =>
        Connections
            .Where(connection => connection.TargetNodeId == nodeId
                                 && (port is null || connection.TargetPort == port))
            .ToArray();
}
