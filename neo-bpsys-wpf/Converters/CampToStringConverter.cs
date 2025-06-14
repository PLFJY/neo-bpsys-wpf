using neo_bpsys_wpf.Enums;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class CampToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Camp camp)
                return string.Empty;

            var campWord = camp == Camp.Sur ? "求生者" : "监管者";

            return $"当前状态：{campWord}";
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
