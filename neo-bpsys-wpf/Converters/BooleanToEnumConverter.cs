using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class BooleanToEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            return value.Equals(parameter);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return parameter;

            return null;
        }
    }
}
