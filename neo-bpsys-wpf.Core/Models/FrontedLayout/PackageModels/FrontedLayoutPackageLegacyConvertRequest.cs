#pragma warning disable CS1591

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageLegacyConvertRequest
{
    public string LegacyPackagePath { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? MinVersion { get; set; }

    public bool InstallAfterConvert { get; set; }

    public bool ActivateAfterInstall { get; set; }
}

public sealed class FrontedLayoutPackageLegacyConvertResult
{
    public bool Success { get; set; }

    public string? ConvertedPackagePath { get; set; }

    public string? InstalledPackageId { get; set; }

    public int LayoutCount { get; set; }

    public int ResourceCount { get; set; }

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public string? ErrorMessage { get; set; }
}

#pragma warning restore CS1591
