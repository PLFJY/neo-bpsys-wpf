using neo_bpsys_wpf.Core.Helpers;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 设计器 v3 属性行的颜色转换帮助程序。
/// </summary>
public static class FrontedPropertyColorHelper
{
    /// <summary>
    /// 存储的颜色字符串无法解析时选择器的回退。
    /// </summary>
    public static Color FallbackColor { get; } = Colors.White;

    /// <summary>
    /// 解析 <c>#RRGGBB</c>、<c>#AARRGGBB</c> 或 WPF 命名颜色字符串而不抛出异常。
    /// RGB 输入被视为完全不透明。
    /// </summary>
    public static bool TryParseArgbColor(string? value, out Color color)
    {
        if (ColorHelper.TryParseColor(value, out color))
        {
            return true;
        }

        color = FallbackColor;
        return false;
    }

    /// <summary>
    /// 将 WPF 颜色格式化为 <c>#AARRGGBB</c>。
    /// </summary>
    public static string ToArgbString(Color color) => color.ToArgbHexString();
}
