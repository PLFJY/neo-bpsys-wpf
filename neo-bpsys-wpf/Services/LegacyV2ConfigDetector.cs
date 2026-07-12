using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 默认的旧版 v2 配置检测器。
/// </summary>
public sealed class LegacyV2ConfigDetector : ILegacyV2ConfigDetector
{
    /// <inheritdoc />
    public bool IsLegacyV2Config(string configJson)
    {
        return SettingsConfigVersionHelper.InspectJson(configJson).IsLegacy;
    }
}
