using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 用于格式化索引的转换器，支持中文和英文环境
/// 用法：
/// 1. 在 XAML 中使用MultiBinding
/// 2. 第一个传格式，通常从 I18N 中获取
/// 3. 第二个传索引，通常从数据源中获取
/// </summary>
public class IndexFormatConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // 验证输入
        if (values.Length < 2)
            return DependencyProperty.UnsetValue;

        // 解析 Rank
        if (!int.TryParse(values[1].ToString(), out var index))
            return "N/A";

        // 获取格式字符串 (来自 I18N)
        var format = values[0].ToString();

        // 核心逻辑：根据格式字符串是否包含 {1} 决定是否需要后缀，中文直接替换
        if (format == null || !format.Contains("{1}")) return format != null ? string.Format(format, index) : "N/A";
        // 英文环境：需要计算后缀 (st/nd/rd/th)
        var suffix = GetOrdinalSuffix(index);
        return string.Format(format, index, suffix);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取数字的序数后缀
    /// </summary>
    /// <param name="number">数字</param>
    /// <returns></returns>
    private static string GetOrdinalSuffix(int number)
    {
        var n = Math.Abs(number) % 100;
        if (n is >= 11 and <= 13) return "th";

        return (n % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }
}