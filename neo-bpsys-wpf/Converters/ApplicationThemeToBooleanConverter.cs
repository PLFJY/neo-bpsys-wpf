using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 将主题和 bool 之间互转，false是Dark，true是Light
/// </summary>
public class ApplicationThemeToBooleanConverter : IValueConverter
{
    /// <summary>
    /// 将 <see cref="ApplicationTheme"/> 转换为对应的布尔值（Dark 为 true，Light 为 false）。
    /// </summary>
    /// <param name="value">要转换的 <see cref="ApplicationTheme"/> 值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>Dark 主题返回 true，Light 主题返回 false</returns>
    /// <exception cref="ArgumentException">当 value 不是 ApplicationTheme 时抛出</exception>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ApplicationTheme applicationTheme)
            throw new ArgumentException();
        return applicationTheme == ApplicationTheme.Dark;
    }

    /// <summary>
    /// 将布尔值反向转换为 <see cref="ApplicationTheme"/>。
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换器参数</param>
    /// <param name="culture">区域性信息</param>
    /// <returns>true 时返回 Dark，false 时返回 Light，null 时返回 DoNothing</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Binding.DoNothing;
        return (bool)value ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }
}