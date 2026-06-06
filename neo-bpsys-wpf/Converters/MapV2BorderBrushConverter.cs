using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// Selects the map card border brush for the current banned state.
/// </summary>
public sealed class MapV2BorderBrushConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var isBanned = values.Length > 0 && values[0] is true;
        var normalBrush = values.Length > 1 ? values[1] as Brush : null;
        var bannedBrush = values.Length > 2 ? values[2] as Brush : null;
        return isBanned ? bannedBrush ?? normalBrush : normalBrush ?? bannedBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
