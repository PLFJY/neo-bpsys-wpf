using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal static class SmartBpPostBpStatusDetector
{
    /// <summary>
    /// 从 OCR 文本行中检测角色 BP 后状态。
    /// </summary>
    /// <param name="lines">待检测的 OCR 文本行。</param>
    /// <returns>检测结果。</returns>
    public static SmartBpPostBpStatusResult Detect(IReadOnlyList<OcrTextLine> lines)
    {
        var rawLines = lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var evidence = string.Join(" / ", rawLines);
        var normalizedLines = rawLines.Select(SmartBpOcrTextResolver.NormalizeForMatch).Where(text => text.Length > 0).ToArray();
        var combined = string.Concat(normalizedLines);
        var auxiliary = new List<string>();
        var hasRemainingSeconds = ContainsFuzzy(combined, "剩余", .5) && combined.Contains('秒');
        if (hasRemainingSeconds) auxiliary.Add("剩余秒");
        var hasGoTo = ContainsFuzzy(combined, "前往", .5);
        var hasBracketedDestination = rawLines.Any(line => Regex.IsMatch(
            line.Normalize(NormalizationForm.FormKC),
            @"[【\[（(《<].+?[】\]）)》>]",
            RegexOptions.CultureInvariant));
        if (hasGoTo && hasBracketedDestination) auxiliary.Add("前往地图");

        var titleMatch = PrimaryTitles
            .Select(candidate => new
            {
                Candidate = candidate,
                Exact = combined.Contains(candidate.Phase, StringComparison.Ordinal),
                TokenMatch = candidate.Tokens.All(token => combined.Contains(token, StringComparison.Ordinal)),
                Similarity = BestSubstringSimilarity(combined, candidate.Phase)
            })
            .Where(match => match.Exact || match.TokenMatch || match.Similarity >= .75)
            .OrderByDescending(match => match.Exact)
            .ThenByDescending(match => match.TokenMatch)
            .ThenByDescending(match => match.Similarity)
            .FirstOrDefault();
        if (titleMatch != null)
        {
            var score = titleMatch.Exact ? 1 : titleMatch.TokenMatch ? .9 : titleMatch.Similarity;
            if (auxiliary.Count > 0)
                score = Math.Min(1, score + .03 * auxiliary.Count);
            var matchMode = titleMatch.Exact ? "exact" : titleMatch.TokenMatch ? "keywords" : "edit-distance";
            return Match(titleMatch.Candidate.Phase, titleMatch.Candidate.Scene, $"{matchMode} title anchor", evidence, combined, auxiliary, score);
        }

        if (hasRemainingSeconds && hasGoTo && hasBracketedDestination)
            return Match("等待游戏开始", SmartBpRecognitionScene.WaitingGameStart,
                "combined auxiliary countdown and destination anchors", evidence, combined, auxiliary, .7);

        return new SmartBpPostBpStatusResult
        {
            Evidence = evidence,
            NormalizedText = combined,
            AuxiliaryEvidence = auxiliary
        };
    }

    private static readonly (string Phase, SmartBpRecognitionScene Scene, string[] Tokens)[] PrimaryTitles =
    [
        ("求生者选择区域中", SmartBpRecognitionScene.AreaSelectionSurvivor, ["求生者", "选择", "区域"]),
        ("监管者选择区域中", SmartBpRecognitionScene.AreaSelectionHunter, ["监管者", "选择", "区域"]),
        ("等待游戏开始", SmartBpRecognitionScene.WaitingGameStart, ["等待", "游戏", "开始"])
    ];

    private static SmartBpPostBpStatusResult Match(
        string phase,
        SmartBpRecognitionScene scene,
        string reason,
        string evidence,
        string normalized,
        IReadOnlyList<string> auxiliary,
        double score) =>
        new()
        {
            IsPostBp = true,
            Phase = phase,
            Scene = scene,
            Reason = reason,
            Evidence = evidence,
            NormalizedText = normalized,
            AuxiliaryEvidence = auxiliary,
            Score = score
        };

    private static bool ContainsFuzzy(string text, string candidate, double threshold) =>
        BestSubstringSimilarity(text, SmartBpOcrTextResolver.NormalizeForMatch(candidate)) >= threshold;

    private static double BestSubstringSimilarity(string text, string candidate)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(candidate)) return 0;
        if (text.Contains(candidate, StringComparison.Ordinal)) return 1;
        var best = 0d;
        var minLength = Math.Max(1, candidate.Length - 1);
        var maxLength = Math.Min(text.Length, candidate.Length + 1);
        for (var length = minLength; length <= maxLength; length++)
        for (var start = 0; start + length <= text.Length; start++)
        {
            var distance = EditDistance(text.AsSpan(start, length), candidate.AsSpan());
            best = Math.Max(best, 1d - (double)distance / Math.Max(length, candidate.Length));
        }
        return best;
    }

    private static int EditDistance(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}

/// <summary>
/// 将单个 OCR 区域文本解析为 SmartBP 聚焦业务识别结果。
/// </summary>
