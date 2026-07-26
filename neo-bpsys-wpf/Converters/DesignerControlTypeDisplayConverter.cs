using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将控件类型字符串转换为本地化显示名称的转换器。
/// </summary>
public sealed class DesignerControlTypeDisplayConverter : IValueConverter
{
    /// <summary>
    /// 将控件类型字符串转换为本地化显示名称。
    /// </summary>
    /// <param name="value">控件类型字符串</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>本地化后的控件类型名称，未找到本地化文本时返回原始类型字符串</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var controlType = value as string;
        if (string.IsNullOrWhiteSpace(controlType))
        {
            return string.Empty;
        }

        var key = $"Designer.ControlType.{controlType}";
        var localized = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? controlType : localized;
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
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
