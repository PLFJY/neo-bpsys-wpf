using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// Module-local localization helper compatible with the host helper shape.
/// </summary>
public static class I18nHelper
{
    /// <summary>
    /// Resolves a localized string by key.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <returns>Localized text, or the key when not found.</returns>
    public static string GetLocalizedString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        var value = LocalizeDictionary.Instance.GetLocalizedObject(key, null, LocalizeDictionary.CurrentCulture);
        return value?.ToString() ?? key;
    }
}
