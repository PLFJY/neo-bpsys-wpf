#pragma warning disable CS1591

using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageImportRequest
{
    public string PackagePath { get; set; } = string.Empty;

    public bool ReplaceExisting { get; set; }

    public bool ActivateAfterImport { get; set; }

    public bool PreserveMissingPlugins { get; set; } = true;
}

public sealed class FrontedLayoutPackageImportResult
{
    public bool Success { get; set; }

    public string? PackageId { get; set; }

    public string? InstalledPath { get; set; }

    public int LayoutCount { get; set; }

    public int ResourceCount { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsLegacyPackage { get; set; }

    public bool RequiresNewerApp { get; set; }

    public bool PackageAlreadyExists { get; set; }

    public bool HasMissingPluginControls => MissingPluginControls.Count > 0;

    public List<FrontedLayoutPackagePluginControlIssue> MissingPluginControls { get; set; } = [];

    public bool HasUnsatisfiedPluginDependencies => UnsatisfiedPluginDependencies.Count > 0;

    public List<FrontedLayoutPackagePluginDependencyIssue> UnsatisfiedPluginDependencies { get; set; } = [];

}

public class FrontedLayoutPackagePluginControlIssue
{
    public string Window { get; set; } = string.Empty;

    public string Canvas { get; set; } = string.Empty;

    public string ControlName { get; set; } = string.Empty;

    public string ControlType { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;
}

public sealed class FrontedLayoutPackagePluginDependencyIssue
{
    public string PackageId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? MinVersion { get; set; }

    public string? InstalledVersion { get; set; }

    public string? MarketplaceId { get; set; }

    public bool IsInstalled { get; set; }

    public bool IsVersionSatisfied { get; set; }

    public bool IsAvailableInMarket { get; set; }

    public bool IsMarketUnavailable { get; set; }

    public List<string> Controls { get; set; } = [];

    public FrontedPluginDependencyReason Reason { get; set; } = FrontedPluginDependencyReason.Unknown;

    public List<string> RequiredBy { get; set; } = [];

    public List<FrontedLayoutPackagePluginControlIssue> AffectedControls { get; set; } = [];
}

#pragma warning restore CS1591
