using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 行为节点图中的连接线。
/// </summary>
public sealed class FrontedNodeConnection
{
    /// <summary>
    /// 连接唯一标识符。
    /// </summary>
    public Guid ConnectionId { get; set; } = FrontedBehaviorGuidHelper.NewGuid();

    /// <summary>
    /// 源节点标识符。
    /// </summary>
    public Guid SourceNodeId { get; set; }

    /// <summary>
    /// 源端口名称。
    /// </summary>
    public string SourcePort { get; set; } = string.Empty;

    /// <summary>
    /// 目标节点标识符。
    /// </summary>
    public Guid TargetNodeId { get; set; }

    /// <summary>
    /// 目标端口名称。
    /// </summary>
    public string TargetPort { get; set; } = string.Empty;
}

