using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.Archives;

/// <summary>
/// 解析 7z.exe -bsp1 进度输出。支持 \r 原地刷新、跨读取块的尾部数字、去重和倒退过滤。
/// </summary>
internal static class SevenZipProgressParser
{
    private static readonly Regex ProgressRegex = new(@"(?<!\d)(\d{1,3})%", RegexOptions.Compiled);

    /// <summary>
    /// 从一段 7z.exe stdout 文本中解析出新的百分比进度。
    /// </summary>
    /// <param name="text">本次读取到的文本(可能包含上一次保留的尾部数字)。</param>
    /// <param name="lastReported">上一次已报告的百分比,传入时按引用更新。倒退或重复的值被忽略。</param>
    /// <returns>
    /// 返回元组:<c>NewPercentages</c> 为本次新报告的百分比列表(已过滤 &gt;100、倒退、重复);
    /// <c>RemainingBuffer</c> 为末尾可能是数字的部分,应与下次读取拼接,用于跨块匹配(如 "5" + "0%" = "50%")。
    /// </returns>
    public static (IReadOnlyList<int> NewPercentages, string RemainingBuffer) Parse(
        string text,
        ref int lastReported)
    {
        var results = new List<int>();
        var matches = ProgressRegex.Matches(text);
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out var percentage))
            {
                continue;
            }

            if (percentage > 100 || percentage <= lastReported)
            {
                continue;
            }

            lastReported = percentage;
            results.Add(percentage);
        }

        var lastMatchEnd = matches.Count > 0 ? matches[^1].Index + matches[^1].Length : 0;
        var remaining = text.Substring(lastMatchEnd);
        var lastNonDigit = remaining.Length;
        while (lastNonDigit > 0 && char.IsDigit(remaining[lastNonDigit - 1]))
        {
            lastNonDigit--;
        }

        var digitCount = remaining.Length - lastNonDigit;
        // 7z 百分比最多 3 位数字,保留末尾最多 3 位用于跨块匹配
        var keepFrom = digitCount > 3 ? remaining.Length - 3 : lastNonDigit;
        var remainingBuffer = keepFrom < remaining.Length
            ? remaining.Substring(keepFrom)
            : string.Empty;

        return (results, remainingBuffer);
    }
}
