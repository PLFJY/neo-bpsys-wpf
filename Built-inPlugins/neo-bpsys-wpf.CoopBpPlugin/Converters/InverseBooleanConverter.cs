using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.CoopBpPlugin.Converters;

/// <summary>
/// 布尔值反转转换器
/// 将 true 转换为 false，将 false 转换为 true
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// 转换布尔值
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return value;
    }

    /// <summary>
    /// 反向转换
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}
