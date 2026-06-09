using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// Request for importing a Designer v3 <c>.bpui</c> layout package.
/// </summary>
public sealed class FrontedLayoutPackageImportRequest
{
    /// <summary>
    /// Package file path selected by the user.
    /// </summary>
    public string PackagePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether an existing installed package with the same package id may be replaced.
    /// </summary>
    public bool ReplaceExisting { get; set; }

    /// <summary>
    /// Whether the package should be activated immediately after installation.
    /// </summary>
    public bool ActivateAfterImport { get; set; }

    /// <summary>
    /// Whether missing plugin controls and plugin window layouts should be preserved instead of removed.
    /// Preserved plugin controls are shown as Designer placeholders and skipped by runtime rendering.
    /// </summary>
    public bool PreserveMissingPlugins { get; set; } = true;
}

/// <summary>
/// Result of importing a Designer v3 <c>.bpui</c> package.
/// </summary>
public sealed class FrontedLayoutPackageImportResult
{
    /// <summary>
    /// Whether the package was imported successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Imported package id when available.
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// Installed package folder path.
    /// </summary>
    public string? InstalledPath { get; set; }

    /// <summary>
    /// Number of layouts installed from the package.
    /// </summary>
    public int LayoutCount { get; set; }

    /// <summary>
    /// Number of resources installed from the package.
    /// </summary>
    public int ResourceCount { get; set; }

    /// <summary>
    /// User-facing or loggable import error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the selected package was detected as a legacy package.
    /// </summary>
    public bool IsLegacyPackage { get; set; }

    /// <summary>
    /// Whether the package manifest requires a newer host app.
    /// </summary>
    public bool RequiresNewerApp { get; set; }

    /// <summary>
    /// Whether an installed package with the same id already exists.
    /// </summary>
    public bool PackageAlreadyExists { get; set; }

    /// <summary>
    /// Whether the package contains controls whose plugin is missing.
    /// </summary>
    public bool HasMissingPluginControls => MissingPluginControls.Count > 0;

    /// <summary>
    /// Missing plugin controls preserved for Designer placeholders or removed on forced import.
    /// </summary>
    public List<FrontedLayoutPackagePluginControlIssue> MissingPluginControls { get; set; } = [];

    /// <summary>
    /// Whether plugin dependencies are missing or below the required version.
    /// </summary>
    public bool HasUnsatisfiedPluginDependencies => UnsatisfiedPluginDependencies.Count > 0;

    /// <summary>
    /// Plugin dependency issues collected from manifest, canvas declarations, and scanned controls.
    /// </summary>
    public List<FrontedLayoutPackagePluginDependencyIssue> UnsatisfiedPluginDependencies { get; set; } = [];

}

/// <summary>
/// Identifies one plugin control that cannot currently be materialized.
/// </summary>
public class FrontedLayoutPackagePluginControlIssue
{
    /// <summary>
    /// Full window type containing the control.
    /// </summary>
    public string Window { get; set; } = string.Empty;

    /// <summary>
    /// Canvas containing the control.
    /// </summary>
    public string Canvas { get; set; } = string.Empty;

    /// <summary>
    /// Control name in the v3 layout JSON.
    /// </summary>
    public string ControlName { get; set; } = string.Empty;

    /// <summary>
    /// Full plugin control type, such as <c>plugin:top.plfjy.example/TeamCard</c>.
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// Missing plugin package id.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;
}

/// <summary>
/// Describes one missing or version-unsatisfied plugin dependency.
/// </summary>
public sealed class FrontedLayoutPackagePluginDependencyIssue
{
    /// <summary>
    /// Required plugin package id.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Optional plugin display name from manifest, installed metadata, or marketplace data.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Minimum required plugin version.
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// Installed plugin version when the plugin is present.
    /// </summary>
    public string? InstalledVersion { get; set; }

    /// <summary>
    /// Marketplace id used by install/update guidance.
    /// </summary>
    public string? MarketplaceId { get; set; }

    /// <summary>
    /// Whether the plugin is already installed.
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Whether the installed plugin version satisfies <see cref="MinVersion"/>.
    /// </summary>
    public bool IsVersionSatisfied { get; set; }

    /// <summary>
    /// Whether the plugin can be found in the configured plugin market.
    /// </summary>
    public bool IsAvailableInMarket { get; set; }

    /// <summary>
    /// Whether market lookup could not be completed.
    /// </summary>
    public bool IsMarketUnavailable { get; set; }

    /// <summary>
    /// Full plugin control types that require this plugin.
    /// </summary>
    public List<string> Controls { get; set; } = [];

    /// <summary>
    /// Why the package needs this plugin.
    /// </summary>
    public FrontedPluginDependencyReason Reason { get; set; } = FrontedPluginDependencyReason.Unknown;

    /// <summary>
    /// Layout windows that require this plugin, formatted as <c>{FullWindowType}</c>.
    /// </summary>
    public List<string> RequiredBy { get; set; } = [];

    /// <summary>
    /// Concrete controls affected by this dependency issue.
    /// </summary>
    public List<FrontedLayoutPackagePluginControlIssue> AffectedControls { get; set; } = [];
}
