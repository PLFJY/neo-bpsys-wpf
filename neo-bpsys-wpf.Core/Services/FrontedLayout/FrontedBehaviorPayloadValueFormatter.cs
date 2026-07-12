using System.Collections;
using System.Globalization;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将行为负载值格式化为用于过滤器和调试的稳定不变文本。
/// </summary>
public static class FrontedBehaviorPayloadValueFormatter
{
    /// <summary>
    /// 使用不变、机器可读的行为过滤语义格式化负载值。
    /// </summary>
    /// <param name="value">要格式化的负载值。</param>
    /// <returns>适合过滤器比较的稳定文本。</returns>
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
