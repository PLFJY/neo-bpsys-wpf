using System.Text;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 一次赛后数据单元格局部识别产生的原始候选。
/// </summary>
/// <param name="Variant">图像预处理变体名称。</param>
/// <param name="RawText">OCR 原始文本。</param>
/// <param name="Confidence">OCR 置信度。</param>
/// <param name="Provider">OCR Provider 名称。</param>
internal sealed record GameDataCellOcrCandidate(
    string Variant,
    string RawText,
    double Confidence,
    string Provider);

/// <summary>
/// 经数字规范化和多变体置信度筛选后的单元格候选。
/// </summary>
/// <param name="Value">规范化后的纯数字值。</param>
/// <param name="Confidence">获胜候选的最高置信度。</param>
/// <param name="SupportCount">支持该值的不同预处理变体数量。</param>
/// <param name="Provider">获胜候选的 OCR Provider 名称。</param>
internal sealed record GameDataCellOcrSelection(
    string Value,
    double Confidence,
    int SupportCount,
    string Provider);

/// <summary>
/// 从多个单元格 OCR 结果中筛选可靠的数字值。
/// </summary>
internal static class GameDataCellOcrCandidateSelector
{
    private const double SingleVariantMinimumConfidence = 0.88;

    /// <summary>
    /// 选择多变体一致或单次置信度足够高的数字结果。
    /// </summary>
    /// <param name="candidates">各预处理变体的 OCR 候选。</param>
    /// <returns>可靠数字候选；没有候选达到接受条件时返回 <see langword="null"/>。</returns>
    internal static GameDataCellOcrSelection? Select(IReadOnlyList<GameDataCellOcrCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var normalized = candidates
            .Select(candidate => TryNormalizeNumericText(candidate.RawText, out var value, out var isExact)
                ? new NormalizedCandidate(candidate, value, isExact)
                : null)
            .Where(item => item != null)
            .Cast<NormalizedCandidate>()
            .ToArray();
        if (normalized.Length == 0)
            return null;

        var winner = normalized
            .GroupBy(item => item.Value!, StringComparer.Ordinal)
            .Select(group => new
            {
                Value = group.Key,
                SupportCount = group.Select(item => item.Candidate.Variant).Distinct(StringComparer.Ordinal).Count(),
                BestExact = group
                    .Where(item => item.IsExact)
                    .OrderByDescending(item => item.Candidate.Confidence)
                    .Select(item => item.Candidate)
                    .FirstOrDefault()
            })
            .Where(group => group.BestExact != null)
            .OrderByDescending(group => group.SupportCount)
            .ThenByDescending(group => group.BestExact?.Confidence ?? 0)
            .FirstOrDefault();

        // 带尾随噪声的候选只能辅助干净数字投票，不能独立触发回填。
        if (winner?.BestExact == null ||
            winner.SupportCount < 2 && winner.BestExact.Confidence < SingleVariantMinimumConfidence)
            return null;

        return new GameDataCellOcrSelection(
            winner.Value,
            Math.Clamp(winner.BestExact.Confidence, 0, 1),
            winner.SupportCount,
            winner.BestExact.Provider);
    }

    /// <summary>
    /// 将 OCR 文本规范化为赛后数据使用的纯数字值。
    /// </summary>
    /// <param name="text">OCR 原始文本。</param>
    /// <returns>纯数字值；文本不是可信数字形式时返回 <see langword="null"/>。</returns>
    internal static string? NormalizeNumericText(string? text)
    {
        return TryNormalizeNumericText(text, out var value, out _) ? value : null;
    }

    /// <summary>
    /// 将 OCR 文本规范化为数字，并区分干净数字与只可用于投票的尾随噪声候选。
    /// </summary>
    /// <param name="text">OCR 原始文本。</param>
    /// <param name="value">规范化后的纯数字值。</param>
    /// <param name="isExact">是否为不含噪声的干净数字候选。</param>
    /// <returns>能够提取可信数字结构时返回 <see langword="true"/>。</returns>
    internal static bool TryNormalizeNumericText(string? text, out string value, out bool isExact)
    {
        value = string.Empty;
        isExact = false;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Normalize(NormalizationForm.FormKC).Trim();
        if (normalized is "I" or "l" or "|" or "丨")
        {
            value = "1";
            isExact = true;
            return true;
        }

        if (normalized.EndsWith('%'))
            normalized = normalized[..^1].TrimEnd();

        if (normalized.Length > 0 && normalized.All(IsAsciiDigit))
        {
            value = normalized;
            isExact = true;
            return true;
        }

        // Paddle 对细窄的 1 偶尔会在尾部多输出一个低分字母（实测为 "1r"）。
        // 这类值只作为第二票使用，Select 会要求同值至少还有一个干净数字候选。
        if (normalized.Length >= 2 &&
            IsAsciiDigit(normalized[^2]) &&
            !IsAsciiDigit(normalized[^1]) &&
            normalized[..^1].All(IsAsciiDigit))
        {
            value = normalized[..^1];
            return true;
        }

        return false;
    }

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';

    private sealed record NormalizedCandidate(
        GameDataCellOcrCandidate Candidate,
        string Value,
        bool IsExact);
}
