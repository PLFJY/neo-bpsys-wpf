#pragma warning disable CS1591

using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IFrontedLayoutPackageManager
{
    Task<IReadOnlyList<FrontedLayoutPackageInfo>> ListPackagesAsync(
        CancellationToken cancellationToken = default);

    Task<FrontedLayoutActivePackageState> GetActivePackageStateAsync(
        CancellationToken cancellationToken = default);

    Task ActivatePackageAsync(string packageId, CancellationToken cancellationToken = default);

    Task<FrontedLayoutPackageInfo> EnsureWritableActivePackageAsync(
        CancellationToken cancellationToken = default);

    Task<FrontedLayoutPackageInfo> DuplicatePackageAsync(
        string sourcePackageId,
        string? requestedName = null,
        CancellationToken cancellationToken = default);

    Task DeletePackageAsync(string packageId, CancellationToken cancellationToken = default);

    string GetPackageLayoutsRootFolder(string packageId);

    string GetPackageLayoutPath(string packageId, string fullWindowType, string canvasName);

    string GetPackageRootFolder();
}

#pragma warning restore CS1591
