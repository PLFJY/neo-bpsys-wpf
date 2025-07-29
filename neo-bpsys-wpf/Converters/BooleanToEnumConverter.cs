using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 转换布尔值到枚举，用于支持RadioButton与枚举类型的双向绑定
/// </summary>
public class BooleanToEnumConverter : IValueConverter
{
    /// <summary>
    /// 将枚举值转换为布尔值，用于确定RadioButton是否选中
    /// </summary>
    /// <param name="value">枚举值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">要比较的枚举值</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>如果value等于parameter则返回true，否则返回false</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 如果值为null，则不选中任何RadioButton
        if (value == null)
            return false;

        // 比较当前枚举值与参数值是否相等
        return value.Equals(parameter);
    }

    /// <summary>
    /// 将布尔值转换为枚举值，用于从RadioButton的选中状态更新枚举值
    /// </summary>
    /// <param name="value">RadioButton的选中状态</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">选中的枚举值</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>如果选中则返回参数值，否则返回null</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 如果RadioButton被选中，则返回对应的枚举值
        if (value is true)
        {
            return parameter;
        }
        
        // 如果RadioButton未被选中，则返回UnsetValue，表示不更新源属性
        // 这可以防止取消选中其他RadioButton时将null值写回源属性
        return null;
    }
}
