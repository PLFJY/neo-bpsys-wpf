using System.Globalization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Color conversion helpers for Designer v3 property rows.
/// </summary>
public static class FrontedPropertyColorHelper
{
    /// <summary>
    /// Picker fallback when a stored color string cannot be parsed.
    /// </summary>
    public static Color FallbackColor { get; } = Colors.White;

    /// <summary>
    /// Parses a <c>#RRGGBB</c> or <c>#AARRGGBB</c> color string without throwing.
    /// RGB input is treated as fully opaque.
    /// </summary>
    public static bool TryParseArgbColor(string? value, out Color color)
    {
        color = FallbackColor;
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

        try
        {
            var offset = text.Length == 8 ? 2 : 0;
            var a = text.Length == 8
                ? byte.Parse(text[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : byte.MaxValue;
            var r = byte.Parse(text.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(text.Substring(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(text.Substring(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            color = Color.FromArgb(a, r, g, b);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    /// Formats a WPF color as <c>#AARRGGBB</c>.
    /// </summary>
    public static string ToArgbString(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
}
