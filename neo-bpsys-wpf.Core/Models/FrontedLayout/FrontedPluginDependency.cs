namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Plugin dependency metadata used by Designer v3 layouts and .bpui manifests.
/// </summary>
public class FrontedPluginDependency
{
    public string PackageId { get; set; } = string.Empty;

    public string? MinVersion { get; set; }

    public string? DisplayName { get; set; }

    public string? MarketplaceId { get; set; }

    public List<string> Controls { get; set; } = [];

    public List<string> RequiredBy { get; set; } = [];
}
