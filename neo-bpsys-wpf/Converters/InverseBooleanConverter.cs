using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 布尔值取反转换器。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// 对布尔值取反。
    /// </summary>
    /// <param name="value">要转换的布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>布尔值的相反值，非布尔值时原样返回</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return value;
    }

    /// <summary>
    /// 反向转换与正向转换一致（取反）。
    /// </summary>
    /// <param name="value">要转换的布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>布尔值的相反值</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}