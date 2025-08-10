using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将主题和 bool 之间互转，false是Dark，true是Light
/// </summary>
public class ApplicationThemeToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ApplicationTheme applicationTheme)
            throw new ArgumentException();
        return applicationTheme == ApplicationTheme.Dark;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Binding.DoNothing;
        return (bool)value ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }
}