namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Add Control catalog group displayed by Designer v3.
/// </summary>
public sealed class FrontedAddControlCatalogGroup
{
    /// <summary>
    /// User-facing group display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Plugin package id when this group hosts plugin controls.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Whether this group hosts plugin controls.
    /// </summary>
    public bool IsPlugin { get; init; }

    /// <summary>
    /// Control items in this group.
    /// </summary>
    public IReadOnlyList<FrontedAddControlCatalogItem> Items { get; init; } = [];
}
