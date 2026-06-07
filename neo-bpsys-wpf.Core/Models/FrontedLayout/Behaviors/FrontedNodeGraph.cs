namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedNodeGraph
{
    public List<FrontedNode> Nodes { get; set; } = [];

    public List<FrontedNodeConnection> Connections { get; set; } = [];
}

