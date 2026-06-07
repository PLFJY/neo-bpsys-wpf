using System.Text.Json;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedNode
{
    public Guid NodeId { get; set; } = FrontedBehaviorGuidHelper.NewGuid();

    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public Dictionary<string, JsonElement> Properties { get; set; } = [];
}

