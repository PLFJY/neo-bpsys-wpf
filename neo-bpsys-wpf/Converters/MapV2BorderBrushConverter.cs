using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// Selects the map card border brush for the current banned state.
/// </summary>
public sealed class MapV2BorderBrushConverter : IMultiValueConverter
{
    /// <summary>
    /// 根据地图是否被 Ban 选择对应的边框画刷。
    /// </summary>
    /// <param name="values">values[0] 为是否被 Ban 的布尔值，values[1] 为正常画刷，values[2] 为 Ban 状态画刷</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>被 Ban 时返回 Ban 状态画刷，否则返回正常画刷</returns>
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var isBanned = values.Length > 0 && values[0] is true;
        var normalBrush = values.Length > 1 ? values[1] as Brush : null;
        var bannedBrush = values.Length > 2 ? values[2] as Brush : null;
        return isBanned ? bannedBrush ?? normalBrush : normalBrush ?? bannedBrush;
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
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
