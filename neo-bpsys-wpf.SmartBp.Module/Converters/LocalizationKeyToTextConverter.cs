using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// Converts a localization key to localized text.
/// </summary>
public sealed class LocalizationKeyToTextConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string key ? I18nHelper.GetLocalizedString(key) : string.Empty;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() ?? string.Empty;
    }
}
