using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// Converts a Boolean value to its inverse.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool boolValue && !boolValue;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool boolValue && !boolValue;
}
