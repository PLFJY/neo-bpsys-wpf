using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Builds the curated binding tree used by Designer v3 Binding Browser.
/// </summary>
public sealed class FrontedBindingBrowserProvider
{
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly IFrontedBindingCatalogProvider _catalogProvider;

    public FrontedBindingBrowserProvider()
        : this(new FrontedDesignerLocalizationService(), new FrontedBindingReflectionCatalogProvider())
    {
    }

    public FrontedBindingBrowserProvider(IFrontedDesignerLocalizationService localizationService)
        : this(localizationService, new FrontedBindingReflectionCatalogProvider())
    {
    }

    public FrontedBindingBrowserProvider(
        IFrontedDesignerLocalizationService localizationService,
        IFrontedBindingCatalogProvider catalogProvider)
    {
        _localizationService = localizationService;
        _catalogProvider = catalogProvider;
    }

    /// <summary>
    /// Builds the complete binding tree without target-type filtering.
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> BuildTree() =>
        BuildTree(FrontedBindingTypeFilter.Any);

    /// <summary>
    /// Builds the binding tree filtered for the expected binding target.
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> BuildTree(FrontedBindingTypeFilter filter)
    {
        return _catalogProvider.BuildCatalog()
            .Select(LocalizeNode)
            .Select(node => FilterNode(node, filter))
            .Where(node => node is not null)
            .Cast<FrontedBindingTreeNode>()
            .ToArray();
    }

    /// <summary>
    /// Searches the complete binding tree without target-type filtering.
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> Search(string? query) =>
        Search(query, FrontedBindingTypeFilter.Any);

    /// <summary>
    /// Searches selectable binding paths filtered for the expected binding target.
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> Search(string? query, FrontedBindingTypeFilter filter)
    {
        var nodes = BuildTree(filter).SelectMany(node => node.Flatten()).Where(node => node.IsSelectable);
        var queryText = query?.Trim();
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return nodes
                .DistinctBy(node => node.FullPath, StringComparer.Ordinal)
                .ToArray();
        }

        return nodes
            .Where(node =>
                node.DisplayName.Contains(queryText, StringComparison.OrdinalIgnoreCase)
                || node.FullPath?.Contains(queryText, StringComparison.OrdinalIgnoreCase) == true)
            .DistinctBy(node => node.FullPath, StringComparer.Ordinal)
            .ToArray();
    }

    private FrontedBindingTreeNode LocalizeNode(FrontedBindingTreeNode node)
    {
        return new FrontedBindingTreeNode
        {
            DisplayName = _localizationService.GetBindingNodeDisplayName(node.DisplayName, node.FullPath),
            FullPath = node.FullPath,
            TypeName = node.TypeName is null
                ? null
                : _localizationService.GetBindingTypeDisplayName(node.TypeName),
            ValueType = node.ValueType,
            Children = node.Children.Select(LocalizeNode).ToArray()
        };
    }

    private static FrontedBindingTreeNode? FilterNode(
        FrontedBindingTreeNode node,
        FrontedBindingTypeFilter filter)
    {
        var children = node.Children
            .Select(child => FilterNode(child, filter))
            .Where(child => child is not null)
            .Cast<FrontedBindingTreeNode>()
            .ToArray();
        var isSelfAllowed = !string.IsNullOrWhiteSpace(node.FullPath) && filter.IsAllowed(node.ValueType);
        if (!isSelfAllowed && children.Length == 0)
        {
            return null;
        }

        return new FrontedBindingTreeNode
        {
            DisplayName = node.DisplayName,
            FullPath = node.FullPath,
            TypeName = node.TypeName,
            ValueType = node.ValueType,
            Children = children
        };
    }

}
