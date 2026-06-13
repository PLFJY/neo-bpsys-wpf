using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Imports Designer v3 fronted layout .bpui packages.
/// </summary>
public interface IFrontedLayoutPackageImporter
{
    /// <summary>
    /// Imports a Designer v3 package archive.
    /// </summary>
    /// <param name="request">Import request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import result.</returns>
    Task<FrontedLayoutPackageImportResult> ImportAsync(
        FrontedLayoutPackageImportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a prepared Designer v3 package directory.
    /// </summary>
    /// <param name="packageDirectory">Directory containing a normal v3 package.</param>
    /// <param name="replaceExisting">Whether an installed package with the same id may be replaced.</param>
    /// <param name="activateAfterImport">Whether to activate the package after installation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import result.</returns>
    Task<FrontedLayoutPackageImportResult> ImportDirectoryAsync(
        string packageDirectory,
        bool replaceExisting,
        bool activateAfterImport,
        CancellationToken cancellationToken = default);
}
