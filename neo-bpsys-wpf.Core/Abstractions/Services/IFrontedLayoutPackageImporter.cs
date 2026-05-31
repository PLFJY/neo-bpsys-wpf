using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Imports Designer v3 fronted layout .bpui packages.
/// </summary>
public interface IFrontedLayoutPackageImporter
{
    Task<FrontedLayoutPackageImportResult> ImportAsync(
        FrontedLayoutPackageImportRequest request,
        CancellationToken cancellationToken = default);
}
