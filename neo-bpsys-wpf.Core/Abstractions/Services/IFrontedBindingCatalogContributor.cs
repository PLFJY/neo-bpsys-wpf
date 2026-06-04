using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Adds virtual or plugin-provided nodes to the Designer v3 binding catalog.
/// </summary>
public interface IFrontedBindingCatalogContributor
{
    /// <summary>
    /// Builds extra binding nodes without reading runtime shared-data values.
    /// </summary>
    IReadOnlyList<FrontedBindingTreeNode> BuildNodes();
}
