using System.Globalization;
using System.Windows;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Converters;
/// <summary>
/// 阵营为Sur时显示，用于MapV2Presenter
/// </summary>
public class CampSurToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Camp camp) return Visibility.Collapsed;

        return camp == Camp.Sur ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
