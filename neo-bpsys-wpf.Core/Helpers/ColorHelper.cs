using System.Globalization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 颜色工具类。
/// </summary>
public static class ColorHelper
{
    public const string DefaultColorHex = "#FFFFFFFF";

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

    public static bool TryParseColor(string? value, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
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

    public static Color ParseColorOrDefault(string? value, Color fallback) =>
        TryParseColor(value, out var color) ? color : fallback;

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

    public static string ToArgbHexString(this Color color) =>
        $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    public static Color ToColor(this string? argb) =>
        ParseColorOrDefault(argb, Colors.White);
}
