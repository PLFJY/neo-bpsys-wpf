using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class ObjectToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Equals(parameter); // 利用模型重写的 Equals 方法
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            return (bool)value ? parameter : Binding.DoNothing;
        }
    }
}
