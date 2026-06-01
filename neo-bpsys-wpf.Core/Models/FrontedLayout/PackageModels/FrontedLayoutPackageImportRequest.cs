#pragma warning disable CS1591

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageImportRequest
{
    public string PackagePath { get; set; } = string.Empty;

    public bool ReplaceExisting { get; set; }

    public bool ActivateAfterImport { get; set; }

    public FrontedLayoutPackageMissingPluginPolicy MissingPluginPolicy { get; set; } =
        FrontedLayoutPackageMissingPluginPolicy.Cancel;
}

public enum FrontedLayoutPackageMissingPluginPolicy
{
    Cancel,
    ForceRemoveMissingControls
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

    public List<FrontedLayoutPackageRemovedPluginControl> RemovedPluginControls { get; set; } = [];
}

public class FrontedLayoutPackagePluginControlIssue
{
    public string Window { get; set; } = string.Empty;

    public string Canvas { get; set; } = string.Empty;

    public string ControlName { get; set; } = string.Empty;

    public string ControlType { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;
}

public sealed class FrontedLayoutPackageRemovedPluginControl : FrontedLayoutPackagePluginControlIssue;

#pragma warning restore CS1591
