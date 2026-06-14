using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 阵营为Hun时显示，用于MapV2Presenter
/// </summary>
public class CampHunToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 当阵营为监管者（Hun）时返回 Visible，否则返回 Collapsed。
    /// </summary>
    /// <param name="value">要转换的 <see cref="Camp"/> 值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>监管者阵营时返回 Visible，否则返回 Collapsed</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Camp camp) return Visibility.Collapsed;

        return camp == Camp.Hun ? Visibility.Visible : Visibility.Collapsed;
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