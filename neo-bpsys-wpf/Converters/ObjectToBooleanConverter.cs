using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将对象与参数比较，相等的转换为 true，用于支持模型 Equals 方法的布尔绑定。
/// </summary>
public class ObjectToBooleanConverter : IValueConverter
{
    /// <summary>
    /// 将对象与参数比较，相等时返回 true。
    /// </summary>
    /// <param name="value">要比较的值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">用于比较的参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>value 与 parameter 相等时返回 true，否则返回 false</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value.Equals(parameter); // 利用模型重写的 Equals 方法
    }

    /// <summary>
    /// 将布尔值反向转换为参数值：true 时返回参数，false 时返回 DoNothing。
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">选中的枚举值</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>true 时返回 parameter 参数值</returns>
    /// <exception cref="InvalidCastException">value 不是 bool 时抛出</exception>
    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    )
    {
        return (bool)value ? parameter : Binding.DoNothing;
    }
}