using System.Globalization;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// Resolves fronted window display names from descriptor-local i18n dictionaries.
/// </summary>
public static class FrontedWindowDisplayNameResolver
{
    /// <summary>
    /// Resolves the user-facing display name for a fronted window descriptor.
    /// </summary>
    /// <param name="descriptor">Window descriptor.</param>
    /// <param name="language">Requested language setting.</param>
    /// <param name="cultureInfo">Effective UI culture used when <paramref name="language"/> is not concrete.</param>
    /// <returns>The localized display name, or the descriptor fallback display name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public static string ResolveDisplayName(
        IFrontedWindowDescriptor descriptor,
        LanguageKey language,
        CultureInfo? cultureInfo = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var concreteLanguage = ResolveConcreteLanguage(language, cultureInfo);
        if (concreteLanguage.HasValue
            && descriptor.I18nDisplayNames is { Count: > 0 } names
            && names.TryGetValue(concreteLanguage.Value, out var localized)
            && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return GetFallbackDisplayName(descriptor);
    }

    /// <summary>
    /// Resolves a concrete supported language from a language setting and effective culture.
    /// </summary>
    /// <param name="language">Requested language setting.</param>
    /// <param name="cultureInfo">Effective UI culture.</param>
    /// <returns>A concrete language key, or <see langword="null"/> when no supported language matches.</returns>
    public static LanguageKey? ResolveConcreteLanguage(LanguageKey language, CultureInfo? cultureInfo = null)
    {
        if (language is not LanguageKey.System and not LanguageKey.FollowApp)
        {
            return language;
        }

        var cultureName = (cultureInfo ?? CultureInfo.CurrentUICulture).Name;
        if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageKey.zh_Hans;
        }

        if (cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageKey.en_US;
        }

        if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageKey.ja_JP;
        }

        return null;
    }

    /// <summary>
    /// Gets the non-localized fallback display name for a fronted window descriptor.
    /// </summary>
    /// <param name="descriptor">Window descriptor.</param>
    /// <returns>The descriptor display name, or its window type name when no display name is configured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public static string GetFallbackDisplayName(IFrontedWindowDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return string.IsNullOrWhiteSpace(descriptor.DisplayName)
            ? descriptor.WindowTypeName
            : descriptor.DisplayName;
    }
}
