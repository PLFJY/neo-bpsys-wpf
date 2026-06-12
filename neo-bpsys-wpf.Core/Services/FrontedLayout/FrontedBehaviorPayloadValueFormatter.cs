using System.Collections;
using System.Globalization;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Formats behavior payload values as stable invariant text for filters and debugging.
/// </summary>
public static class FrontedBehaviorPayloadValueFormatter
{
    /// <summary>
    /// Formats a payload value using invariant, machine-readable behavior filter semantics.
    /// </summary>
    /// <param name="value">Payload value to format.</param>
    /// <returns>Stable text suitable for filter comparisons.</returns>
    public static string Format(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is Enum enumValue)
        {
            return enumValue.ToString();
        }

        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }

        if (value is IFormattable formattable && value is not IEnumerable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (value is IEnumerable enumerable)
        {
            var items = enumerable
                .Cast<object?>()
                .Select(Format)
                .ToArray();
            return $"[{string.Join(", ", items)}]";
        }

        return value.ToString() ?? string.Empty;
    }
}
