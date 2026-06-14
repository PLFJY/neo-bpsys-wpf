using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将本地化键转换为本地化文本的转换器。
/// </summary>
public class LocalizationKeyToTextConverter : IValueConverter
{
    /// <summary>
    /// 将本地化键转换为本地化文本。
    /// </summary>
    /// <param name="value">本地化键（字符串）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>本地化文本，未找到时返回原始键</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var localized = I18nHelper.GetLocalizedString(key);
        return string.IsNullOrWhiteSpace(localized) ? key : localized;
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
