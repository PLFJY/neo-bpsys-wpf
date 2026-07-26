using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将延迟毫秒数（<see cref="int"/>?）转换为对应的 <see cref="SolidColorBrush"/>。
/// </summary>
public class LatencyToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush BlueBrush = new(Colors.DodgerBlue);
    private static readonly SolidColorBrush OrangeBrush = new(Colors.Orange);
    private static readonly SolidColorBrush RedBrush = new(Colors.Red);
    private static readonly SolidColorBrush GrayBrush = new(Colors.Gray);

    /// <summary>
    /// 将 <see cref="int"/>? 延迟值转换为其对应的颜色画刷。
    /// </summary>
    /// <param name="value">延迟毫秒数（可为 null）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数（未使用）</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>对应颜色的 <see cref="SolidColorBrush"/></returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int latencyMs || latencyMs <= 0)
        {
            return GrayBrush;
        }

        return latencyMs switch
        {
            < 200 => GreenBrush,
            < 500 => BlueBrush,
            < 1000 => OrangeBrush,
            _ => RedBrush
        };
    }

    /// <summary>
    /// 反向转换（不支持）。
    /// </summary>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
