using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// MultiValueConverter：判断 ComboBoxItem 是否可启用。
/// values[0] = KeyValuePair{string, Character}（当前项）
/// values[1] = ISet{string}（应禁用的角色名称集合）
/// 返回 true 表示可启用，false 表示禁用
/// </summary>
public class ComboBoxItemIsEnabledConverter : IMultiValueConverter
{
    /// <summary>
    /// 判断 ComboBoxItem 是否可启用。
    /// </summary>
    /// <param name="values">values[0] 为 KeyValuePair{string, Character}（当前项），values[1] 为 ISet{string}（应禁用的角色名称集合）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>当前项不在禁用集合中时返回 true，否则返回 false</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [KeyValuePair<string, Character> kvp, ISet<string> disabled])
            return !disabled.Contains(kvp.Key);
        return true;
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="targetTypes">目标类型数组</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>不支持</returns>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
