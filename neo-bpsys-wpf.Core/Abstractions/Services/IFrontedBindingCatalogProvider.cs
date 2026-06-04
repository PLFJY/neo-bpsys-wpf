using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides the Designer v3 binding catalog.
/// </summary>
public interface IFrontedBindingCatalogProvider
{
    /// <summary>
    /// Builds or returns the cached binding catalog.
    /// </summary>
    IReadOnlyList<FrontedBindingTreeNode> BuildCatalog();
}
