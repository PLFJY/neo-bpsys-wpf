using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 阵营枚举转换成中文，用于TeamInfoPage
/// </summary>
public class CampToStringConverter : IValueConverter
{
    /// <summary>
    /// 将阵营枚举值转换为中文字符串。
    /// </summary>
    /// <param name="value">要转换的 <see cref="Camp"/> 值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>求生者返回"求生者"，监管者返回"监管者"，其他返回空字符串</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Camp camp)
            return string.Empty;

        var campWord = camp == Camp.Sur ? "求生者" : "监管者";

        return campWord;
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
    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}