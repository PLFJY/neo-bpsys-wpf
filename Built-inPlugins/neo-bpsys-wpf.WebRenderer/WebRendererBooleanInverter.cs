using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.WebRenderer;

/// <summary>用于管理页生命周期按钮的布尔取反转换器。</summary>
public sealed class WebRendererBooleanInverter : IValueConverter
{
    /// <summary>将布尔值取反以供控件启用状态使用。</summary>
    /// <param name="value">源值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">转换参数。</param>
    /// <param name="culture">区域性信息。</param>
    /// <returns>取反后的布尔值；非布尔值保持不变。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool flag ? !flag : value;

    /// <summary>将布尔值取反以支持双向绑定。</summary>
    /// <param name="value">源值。</param>
    /// <param name="targetType">目标类型。</param>
    /// <param name="parameter">转换参数。</param>
    /// <param name="culture">区域性信息。</param>
    /// <returns>取反后的布尔值；非布尔值保持不变。</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Convert(value, targetType, parameter, culture);
}
