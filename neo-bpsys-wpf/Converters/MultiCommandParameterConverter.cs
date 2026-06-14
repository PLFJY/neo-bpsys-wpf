using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 多参数命令参数转换器
/// </summary>
public class MultiCommandParameterConverter : IMultiValueConverter
{
    /// <summary>
    /// 转换
    /// </summary>
    /// <param name="values">源参数</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数</param>
    /// <param name="culture">区域</param>
    /// <returns>转换后的参数</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values.Clone();
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="targetTypes">目标类型数组</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>不支持</returns>
    /// <exception cref="NotImplementedException">始终抛出</exception>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}