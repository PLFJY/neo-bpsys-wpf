using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// Converts a Boolean value to <see cref="Visibility.Visible"/> or <see cref="Visibility.Hidden"/>.
/// </summary>
public sealed class BooleanToHiddenVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Hidden;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
