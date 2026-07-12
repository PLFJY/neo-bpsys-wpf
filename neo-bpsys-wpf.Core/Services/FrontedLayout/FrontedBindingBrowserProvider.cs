using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 构建 Designer v3 绑定浏览器使用的精选绑定树。
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
    /// 构建完整绑定树，不进行目标类型筛选。
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> BuildTree() =>
        BuildTree(FrontedBindingTypeFilter.Any);

    /// <summary>
    /// 构建按预期绑定目标筛选的绑定树。
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
    /// 搜索完整绑定树，不进行目标类型筛选。
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> Search(string? query) =>
        Search(query, FrontedBindingTypeFilter.Any);

    /// <summary>
    /// 搜索按预期绑定目标筛选的可选绑定路径。
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
