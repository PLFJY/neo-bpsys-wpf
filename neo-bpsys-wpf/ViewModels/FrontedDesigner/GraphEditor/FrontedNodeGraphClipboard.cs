using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

/// <summary>
/// 存储复制的图节点，以便在不同动画阶段之间执行粘贴操作。
/// </summary>
public static class FrontedNodeGraphClipboard
{
    /// <summary>获取或设置当前应用级图剪贴板负载。</summary>
    public static FrontedNodeGraphClipboardPayload? Payload { get; set; }
}

/// <summary>
/// 表示带版本的图剪贴板负载。
/// </summary>
public sealed class FrontedNodeGraphClipboardPayload
{
    /// <summary>获取或设置负载架构版本。</summary>
    public int Version { get; set; } = 1;

    /// <summary>获取或设置已复制的节点。</summary>
    public List<FrontedNode> Nodes { get; set; } = [];

    /// <summary>获取或设置两端节点均被复制的连接。</summary>
    public List<FrontedNodeConnection> Connections { get; set; } = [];
}
