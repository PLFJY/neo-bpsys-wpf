namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Add Control catalog entry for a built-in or plugin fronted control.
/// </summary>
public sealed class FrontedAddControlCatalogItem
{
    public string ControlType { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Icon { get; init; }

    public bool IsPlugin { get; init; }

    public string? PackageId { get; init; }

    public string? PluginDisplayName { get; init; }

    public bool IsAvailable { get; init; } = true;

    public string? UnavailableReason { get; init; }
}
