using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 布尔值转换为可见性
    /// <para>true 对应 Collapsed</para>
    /// <para>false 对应 Visible</para>
    /// </summary>
    public class BooleanToReverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}
