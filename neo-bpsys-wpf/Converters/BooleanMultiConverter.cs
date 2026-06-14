using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 转换多个布尔值到一个布尔值，用于合并待选框1和2的，一个与门
/// </summary>
public class BooleanMultiConverter : IMultiValueConverter
{
    /// <summary>
    /// 对多个布尔值执行逻辑与运算，所有值均为 true 时返回 true。
    /// </summary>
    /// <param name="values">要转换的布尔值数组</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>所有值均为 true 时返回 true，否则返回 false</returns>
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

    /// <summary>
    /// 将单个布尔值反向转换为与目标类型数量相等的布尔值数组。
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetTypes">目标类型数组</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>与目标类型数量相等的布尔值数组</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return [.. targetTypes.Select(_ => (object)boolValue)];
        }

        return [false, false];
    }
}