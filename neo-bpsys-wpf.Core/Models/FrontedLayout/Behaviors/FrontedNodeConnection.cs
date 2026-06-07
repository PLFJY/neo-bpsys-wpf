using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedNodeConnection
{
    public Guid ConnectionId { get; set; } = FrontedBehaviorGuidHelper.NewGuid();

    public Guid SourceNodeId { get; set; }

    public string SourcePort { get; set; } = string.Empty;

    public Guid TargetNodeId { get; set; }

    public string TargetPort { get; set; } = string.Empty;
}

