using neo_bpsys_wpf.Core.Helpers;
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
    /// Parses a <c>#RRGGBB</c>, <c>#AARRGGBB</c>, or WPF named color string without throwing.
    /// RGB input is treated as fully opaque.
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
    /// Formats a WPF color as <c>#AARRGGBB</c>.
    /// </summary>
    public static string ToArgbString(Color color) => color.ToArgbHexString();
}
