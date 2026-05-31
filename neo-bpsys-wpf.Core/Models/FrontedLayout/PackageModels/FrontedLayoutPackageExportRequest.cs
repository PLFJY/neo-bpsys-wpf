#pragma warning disable CS1591

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

public sealed class FrontedLayoutPackageExportRequest
{
    public string PackageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string MinVersion { get; set; } = string.Empty;

    public FrontedLayoutPackageExportScope ExportScope { get; set; } = FrontedLayoutPackageExportScope.AllFrontendLayouts;

    public string OutputPath { get; set; } = string.Empty;

    public string? WindowTypeName { get; set; }

    public string? CanvasName { get; set; }
}

public enum FrontedLayoutPackageExportScope
{
    CurrentCanvas,
    CurrentWindow,
    AllFrontendLayouts
}

public sealed class FrontedLayoutPackageExportResult
{
    public bool Success { get; set; }

    public string OutputPath { get; set; } = string.Empty;

    public int LayoutCount { get; set; }

    public int ResourceCount { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;
}

#pragma warning restore CS1591
