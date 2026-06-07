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
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [KeyValuePair<string, Character> kvp, ISet<string> disabled])
            return !disabled.Contains(kvp.Key);
        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
