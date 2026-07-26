using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 字符串与双精度浮点数之间的双向转换器。
/// </summary>
public class StringToDoubleConverter : IValueConverter
{
    /// <summary>
    /// 将值转换为字符串表示。
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>值的字符串表示</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 将字符串转换为双精度浮点数。
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>解析后的 double 值，解析失败时返回 0</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string str) return 0;
        if (double.TryParse(str, out double result)) return result;
        return 0;
    }
}