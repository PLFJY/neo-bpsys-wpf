using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 转换多个布尔值到一个布尔值，用于合并待选框1和2的
/// </summary>
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
            return [.. targetTypes.Select(t => (object)boolValue)];
        }

        return [false, false]; // 默认值，适用于两个源属性的情况
    }
}