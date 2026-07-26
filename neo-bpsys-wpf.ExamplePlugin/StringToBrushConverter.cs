using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.ExamplePlugin;

/// <summary>
/// 将颜色字符串（如 <c>#FFFFFFFF</c>）转换为 <see cref="Brush"/> 的 XAML 绑定转换器。
/// </summary>
/// <remarks>
/// 该转换器用于 <see cref="TeamCardControl"/> 的 XAML 绑定，使 Options 视图中以字符串形式存储的
/// 颜色值能直接绑定到 <see cref="System.Windows.Controls.TextBlock.Foreground"/> 等依赖属性。
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>
    /// 将颜色字符串转换为 <see cref="Brush"/>；解析失败时返回白色画刷。
    /// </summary>
    /// <param name="value">颜色字符串。</param>
    /// <param name="targetType">目标类型（应为 <see cref="Brush"/>）。</param>
    /// <param name="parameter">未使用。</param>
    /// <param name="culture">未使用。</param>
    /// <returns>与颜色字符串对应的 <see cref="SolidColorBrush"/>。</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ColorHelper.CreateBrushOrDefault(value as string, Colors.White);
    }

    /// <summary>
    /// 将 <see cref="Brush"/> 转换回颜色字符串；仅支持 <see cref="SolidColorBrush"/>。
    /// </summary>
    /// <param name="value">画刷实例。</param>
    /// <param name="targetType">目标类型（应为 <see cref="string"/>）。</param>
    /// <param name="parameter">未使用。</param>
    /// <param name="culture">未使用。</param>
    /// <returns>颜色字符串；无法转换时返回 <see cref="ColorHelper.DefaultColorHex"/>。</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color.ToArgbHexString();
        }

        return ColorHelper.DefaultColorHex;
    }
}
