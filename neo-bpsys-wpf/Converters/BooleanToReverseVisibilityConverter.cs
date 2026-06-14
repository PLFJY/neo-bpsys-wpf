using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 布尔值转换为相反的可见性
/// <para>true 对应 Collapsed</para>
/// <para>false 对应 Visible</para>
/// </summary>
public class BooleanToReverseVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 将布尔值转换为相反的可见性：true 为 Collapsed，false 为 Visible。
    /// </summary>
    /// <param name="value">要转换的布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>true 时返回 Collapsed，false 时返回 Visible</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Visibility.Collapsed : Visibility.Visible;
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
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}