using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

public class LocalizationKeyToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var localized = I18nHelper.GetLocalizedString(key);
        return string.IsNullOrWhiteSpace(localized) ? key : localized;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
