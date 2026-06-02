namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Plugin dependency metadata used by Designer v3 layouts and .bpui manifests.
/// </summary>
public class FrontedPluginDependency
{
    /// <summary>
    /// Plugin package id required by the layout or package.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Minimum plugin package version required to safely render or edit the layout.
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// Optional display name copied from installed plugin metadata or package manifest.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional marketplace id used by the install/update guidance UI.
    /// </summary>
    public string? MarketplaceId { get; set; }

    /// <summary>
    /// Why this dependency exists, such as plugin controls, plugin windows, or both.
    /// </summary>
    public FrontedPluginDependencyReason Reason { get; set; } = FrontedPluginDependencyReason.Unknown;

    /// <summary>
    /// Full plugin control types that require this plugin.
    /// </summary>
    public List<string> Controls { get; set; } = [];

    /// <summary>
    /// Layout locations that require this plugin, formatted as <c>{FullWindowType}/{CanvasName}</c>.
    /// Missing plugin window layouts are preserved in packages but not loaded until the plugin is installed.
    /// </summary>
    public List<string> RequiredBy { get; set; } = [];
}
