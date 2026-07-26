using System.Text.Json;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 行为节点图中的节点。
/// </summary>
public sealed class FrontedNode
{
    /// <summary>
    /// 节点唯一标识符。
    /// </summary>
    public Guid NodeId { get; set; } = FrontedBehaviorGuidHelper.NewGuid();

    /// <summary>
    /// 节点类型。
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点显示名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 节点在画布中的 X 坐标。
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// 节点在画布中的 Y 坐标。
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 节点属性集合。
    /// </summary>
    public Dictionary<string, JsonElement> Properties { get; set; } = [];
}

