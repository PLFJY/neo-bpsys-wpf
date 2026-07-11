using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// Resolves a dynamic localization key against an explicitly selected host resource dictionary.
/// </summary>
/// <remarks>
/// This converter is intended for WPF data templates, where <c>lex:Loc</c> cannot reliably
/// inherit provider context while its dynamic key binding is being created.
/// </remarks>
public sealed class DictionaryLocalizationConverter : IValueConverter
{
    /// <summary>
    /// Resolves the supplied key using the dictionary supplied through <paramref name="parameter"/>.
    /// </summary>
    /// <param name="value">The dynamic localization key.</param>
    /// <param name="targetType">The target property type.</param>
    /// <param name="parameter">An <see cref="AppI18nDictionaries"/> dictionary name.</param>
    /// <param name="culture">The culture requested by the WPF binding engine.</param>
    /// <returns>The localized value, or the original key when it cannot be resolved.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as string ?? System.Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (parameter is not string dictionary || string.IsNullOrWhiteSpace(dictionary))
        {
            return key;
        }

        return I18nHelper.GetLocalizedString(dictionary, key, culture);
    }

    /// <summary>
    /// Does not support reverse conversion.
    /// </summary>
    /// <param name="value">The value to convert back.</param>
    /// <param name="targetType">The requested source type.</param>
    /// <param name="parameter">The converter parameter.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>This member never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown because localization keys cannot be inferred from display text.</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

}
