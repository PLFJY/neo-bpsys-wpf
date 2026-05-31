using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// I18n-backed Designer v3 localization service for the WPF host.
/// </summary>
public sealed class FrontedDesignerI18nLocalizationService : FrontedDesignerLocalizationService
{
    protected override string GetLocalizedOrFallback(string key, string fallback)
    {
        var localized = I18nHelper.GetLocalizedString(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
    }
}
