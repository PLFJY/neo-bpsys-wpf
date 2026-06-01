namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Add Control catalog group displayed by Designer v3.
/// </summary>
public sealed class FrontedAddControlCatalogGroup
{
    public string DisplayName { get; init; } = string.Empty;

    public string? PackageId { get; init; }

    public bool IsPlugin { get; init; }

    public IReadOnlyList<FrontedAddControlCatalogItem> Items { get; init; } = [];
}
