using System.Globalization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 颜色工具类。
/// </summary>
public static class ColorHelper
{
    public const string DefaultColorHex = "#FFFFFFFF";

    
    /// <summary>
    /// 标准化颜色值，失败时返回默认值。
    /// </summary>
    /// <param name="value">输入的颜色字符串</param>
    /// <param name="normalized">标准化后的颜色字符串</param>
    /// <returns>标准化后的颜色字符串</returns>
    public static bool TryNormalizeHex(string? value, out string normalized)
    {
        normalized = DefaultColorHex;
        if (!TryParseColor(value, out var color))
        {
            return false;
        }

        normalized = color.ToArgbHexString();
        return true;
    }

    /// <summary>
    /// 标准化颜色值，失败时返回默认值。
    /// </summary>
    /// <param name="value">输入的颜色字符串</param>
    /// <param name="defaultValue">默认颜色值</param>
    /// <returns>标准化后的颜色字符串</returns>
    public static string NormalizeHexOrDefault(
        string? value,
        string defaultValue = DefaultColorHex)
    {
        if (TryNormalizeHex(value, out var normalized))
        {
            return normalized;
        }

        return TryNormalizeHex(defaultValue, out normalized)
            ? normalized
            : DefaultColorHex;
    }

    /// <summary>
    /// Tries to parse a color from <c>#RRGGBB</c>, <c>#AARRGGBB</c>, or a WPF named color.
    /// </summary>
    /// <param name="value">The color text to parse.</param>
    /// <param name="color">The parsed color when parsing succeeds.</param>
    /// <returns><c>true</c> when the value is a supported color; otherwise <c>false</c>.</returns>
    public static bool TryParseColor(string? value, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (!text.StartsWith('#'))
        {
            try
            {
                var converted = ColorConverter.ConvertFromString(text);
                if (converted is Color namedColor)
                {
                    color = namedColor;
                    return true;
                }
            }
            catch (FormatException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            return false;
        }

        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length is not (6 or 8))
        {
            return false;
        }

        var offset = text.Length == 8 ? 2 : 0;
        if (!byte.TryParse(
                text.Length == 8 ? text[..2] : "FF",
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var a)
            || !byte.TryParse(text.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(text.Substring(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(text.Substring(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    /// <summary>
    /// 解析颜色字符串，失败时返回指定的回退值。
    /// </summary>
    /// <param name="value">输入的颜色字符串</param>
    /// <param name="fallback">解析失败时的回退颜色</param>
    /// <returns>解析到的颜色或回退值</returns>
    public static Color ParseColorOrDefault(string? value, Color fallback) =>
        TryParseColor(value, out var color) ? color : fallback;

    /// <summary>
    /// 解析颜色字符串并创建 <see cref="SolidColorBrush"/>，失败时使用指定的回退颜色。
    /// </summary>
    /// <param name="value">输入的颜色字符串</param>
    /// <param name="fallback">解析失败时的回退颜色</param>
    /// <returns>创建的画刷</returns>
    public static SolidColorBrush CreateBrushOrDefault(string? value, Color fallback) =>
        new(ParseColorOrDefault(value, fallback));

    /// <summary>
    /// 将16进制ARGB或RGB颜色字符串转换为SolidColorBrush。
    /// </summary>
    public static SolidColorBrush HexToBrush(string hexColor)
    {
        if (!TryParseColor(hexColor, out var color))
        {
            throw new ArgumentException("颜色字符串格式不正确，应为#RRGGBB或#AARRGGBB格式", nameof(hexColor));
        }

        return new SolidColorBrush(color);
    }

    /// <summary>
    /// 将 <see cref="Color"/> 转换为 #AARRGGBB 十六进制字符串。
    /// </summary>
    /// <param name="color">颜色值</param>
    /// <returns>十六进制颜色字符串</returns>
    public static string ToArgbHexString(this Color color) =>
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// 将颜色字符串转换为 <see cref="Color"/>，无法解析时返回白色。
    /// </summary>
    /// <param name="argb">颜色字符串</param>
    /// <returns>解析到的颜色或白色</returns>
    public static Color ToColor(this string? argb) =>
        ParseColorOrDefault(argb, Colors.White);
}
