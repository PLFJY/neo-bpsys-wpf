using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 根据显式选择的宿主资源字典解析动态本地化键。
/// </summary>
/// <remarks>
/// 此转换器适用于 WPF 数据模板，因为在创建动态键绑定时，<c>lex:Loc</c> 无法可靠地继承提供程序上下文。
/// </remarks>
public sealed class DictionaryLocalizationConverter : IValueConverter
{
    /// <summary>
    /// 使用通过 <paramref name="parameter"/> 传入的字典解析给定键。
    /// </summary>
    /// <param name="value">动态本地化键。</param>
    /// <param name="targetType">目标属性类型。</param>
    /// <param name="parameter"><see cref="AppI18nDictionaries"/> 字典名称。</param>
    /// <param name="culture">WPF 绑定引擎请求的区域性。</param>
    /// <returns>本地化后的值；无法解析时返回原始键。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as string ?? System.Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (parameter is not string dictionary || string.IsNullOrWhiteSpace(dictionary))
        {
            return key;
        }

        return I18nHelper.GetLocalizedString(dictionary, key, culture);
    }

    /// <summary>
    /// 不支持反向转换。
    /// </summary>
    /// <param name="value">要反向转换的值。</param>
    /// <param name="targetType">请求的源类型。</param>
    /// <param name="parameter">转换器参数。</param>
    /// <param name="culture">请求的区域性。</param>
    /// <returns>此成员永远不会返回。</returns>
    /// <exception cref="NotSupportedException">始终抛出，因为无法从显示文本推断本地化键。</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

}
