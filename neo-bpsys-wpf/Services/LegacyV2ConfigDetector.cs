using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// Default legacy v2 config detector.
/// </summary>
public sealed class LegacyV2ConfigDetector : ILegacyV2ConfigDetector
{
    /// <inheritdoc />
    public bool IsLegacyV2Config(string configJson)
    {
        return SettingsConfigVersionHelper.InspectJson(configJson).IsLegacy;
    }
}
