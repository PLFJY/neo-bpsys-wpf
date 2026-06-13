using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

/// <summary>
/// Stores copied graph nodes for paste operations across animation stages.
/// </summary>
public static class FrontedNodeGraphClipboard
{
    /// <summary>Gets or sets the current app-level graph clipboard payload.</summary>
    public static FrontedNodeGraphClipboardPayload? Payload { get; set; }
}

/// <summary>
/// Represents a versioned graph clipboard payload.
/// </summary>
public sealed class FrontedNodeGraphClipboardPayload
{
    /// <summary>Gets or sets the payload schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the copied nodes.</summary>
    public List<FrontedNode> Nodes { get; set; } = [];

    /// <summary>Gets or sets connections whose endpoints are both copied nodes.</summary>
    public List<FrontedNodeConnection> Connections { get; set; } = [];
}
