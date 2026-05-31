using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

public sealed class DesignerControlTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var controlType = value as string;
        if (string.IsNullOrWhiteSpace(controlType))
        {
            return string.Empty;
        }

        var key = $"Designer.ControlType.{controlType}";
        var localized = I18nHelper.GetLocalizedString(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? controlType : localized;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
