using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将Double的Spacing值转换为Margin的Right值
    /// </summary>
    public class DoubleToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double spacing)
            {
                return new Thickness(0, 0, spacing, 0);
            }
            return new Thickness();
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
