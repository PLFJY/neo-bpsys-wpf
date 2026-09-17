using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 按队员删除操作原有的文本格式拼接本地化提示和队员姓名。
/// </summary>
public sealed class MemberDeleteConfirmationTextConverter : IMultiValueConverter
{
    /// <summary>
    /// 将本地化提示与可选队员姓名拼接为删除确认文本。
    /// </summary>
    /// <param name="values">第一项为本地化提示，第二项为队员姓名。</param>
    /// <param name="targetType">目标属性类型。</param>
    /// <param name="parameter">未使用的转换器参数。</param>
    /// <param name="culture">绑定使用的区域性。</param>
    /// <returns>保持迁移前格式的删除确认文本。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return DependencyProperty.UnsetValue;
        }

        var prompt = values[0]?.ToString() ?? string.Empty;
        var memberName = values[1] == DependencyProperty.UnsetValue
            ? string.Empty
            : values[1]?.ToString() ?? string.Empty;
        var formattedMemberName = string.IsNullOrEmpty(memberName)
            ? string.Empty
            : $" \"{memberName}\" ";

        return $"{prompt} {formattedMemberName}？";
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">要反向转换的值。</param>
    /// <param name="targetTypes">请求的源类型。</param>
    /// <param name="parameter">未使用的转换器参数。</param>
    /// <param name="culture">绑定使用的区域性。</param>
    /// <returns>此成员永远不会返回。</returns>
    /// <exception cref="NotSupportedException">始终抛出，因为无法从确认文本还原提示和姓名。</exception>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
