using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将Double的Spacing值转换为Margin的Right值
/// </summary>
public class DoubleToThicknessConverter : IValueConverter
{
    /// <summary>
    /// 将 Double 值转换为 Margin 的 Right 值。
    /// </summary>
    /// <param name="value">间距值（Double 类型）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>Right 为指定间距值的 Thickness，其他边为 0</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double spacing)
        {
            return new Thickness(0, 0, spacing, 0);
        }
        return new Thickness();
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>不支持</returns>
    /// <exception cref="NotImplementedException">始终抛出</exception>
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