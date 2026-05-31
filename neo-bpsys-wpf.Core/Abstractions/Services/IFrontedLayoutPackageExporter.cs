using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Exports Designer v3 fronted layouts as .bpui packages.
/// </summary>
public interface IFrontedLayoutPackageExporter
{
    Task<FrontedLayoutPackageExportResult> ExportAsync(
        FrontedLayoutPackageExportRequest request,
        CancellationToken cancellationToken = default);
}
