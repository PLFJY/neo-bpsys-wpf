using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Converts legacy .bpui packages into Designer v3 .bpui packages.
/// </summary>
public interface IFrontedLayoutPackageLegacyConverter
{
    /// <summary>
    /// Converts a legacy package into a clean v3 package and optionally installs it.
    /// </summary>
    Task<FrontedLayoutPackageLegacyConvertResult> ConvertAsync(
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken = default);
}
