using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

public class BooleanMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0)
            return false;

        foreach (var value in values)
        {
            if (value is bool and false)
                return false;
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return targetTypes.Select(t => (object)boolValue).ToArray();
        }

        return [false, false]; // 默认值，适用于两个源属性的情况
    }
}