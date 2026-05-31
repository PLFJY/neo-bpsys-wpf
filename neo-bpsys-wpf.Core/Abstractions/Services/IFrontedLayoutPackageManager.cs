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

    Task DeletePackageAsync(string packageId, CancellationToken cancellationToken = default);

    string GetPackageRootFolder();
}

#pragma warning restore CS1591
