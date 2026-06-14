namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Add Control catalog entry for a built-in or plugin fronted control.
/// </summary>
public sealed class FrontedAddControlCatalogItem
{
    /// <summary>
    /// Control type identifier.
    /// </summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>
    /// User-facing display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// User-facing description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Optional icon key.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Whether this control comes from a plugin.
    /// </summary>
    public bool IsPlugin { get; init; }

    /// <summary>
    /// Plugin package id when <see cref="IsPlugin"/> is true.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Plugin display name when <see cref="IsPlugin"/> is true.
    /// </summary>
    public string? PluginDisplayName { get; init; }

    /// <summary>
    /// Whether the control is currently available to add.
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// Human-readable reason when the control is unavailable.
    /// </summary>
    public string? UnavailableReason { get; init; }
}
