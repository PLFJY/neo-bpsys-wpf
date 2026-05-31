#pragma warning disable CS1591

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageInfo
{
    public string PackageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTimeOffset? CreatedAt { get; set; }

    public string MinVersion { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public FrontedLayoutPackageSource Source { get; set; }

    public bool IsBuiltin { get; set; }

    public bool IsLocal { get; set; }

    public bool IsActive { get; set; }

    public int LayoutCount { get; set; }

    public int ResourceCount { get; set; }

    public FrontedLayoutPackageValidationStatus ValidationStatus { get; set; }

    public string ValidationMessage { get; set; } = string.Empty;
}

#pragma warning restore CS1591
