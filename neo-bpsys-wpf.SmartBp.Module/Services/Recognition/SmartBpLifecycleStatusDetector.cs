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

internal sealed class SmartBpLifecycleStatusDetector : ISmartBpLifecycleStatusDetector
{
    private const double WeakThreshold = .65;

    /// <inheritdoc />
    public SmartBpLifecycleStatusResult Detect(IReadOnlyList<OcrTextLine> lines)
    {
        var rawLines = lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var evidence = string.Join(" / ", rawLines);
        var normalized = string.Concat(rawLines.Select(SmartBpOcrTextResolver.NormalizeForMatch));
        var hasDestination = BestSubstringSimilarity(normalized, "前往") >= .5;
        var matches = Candidates.Select(candidate =>
        {
            var phraseScore = BestSubstringSimilarity(normalized, candidate.Status);
            var keywordHits = candidate.Tokens.Count(token =>
                BestSubstringSimilarity(normalized, token) >= (token.Length <= 2 ? .5 : .67));
            var coverage = (double)keywordHits / candidate.Tokens.Length;
            var score = phraseScore * .72 + coverage * .28 + (hasDestination ? .03 : 0);
            return new { Candidate = candidate, PhraseScore = phraseScore, Coverage = coverage, Score = Math.Min(1, score) };
        }).OrderByDescending(match => match.Score).First();

        var recognized = matches.Score >= WeakThreshold && matches.Coverage >= .5;
        var status = recognized ? matches.Candidate.Status : "未知";
        var category = recognized ? matches.Candidate.Category : SmartBpLifecycleCategory.Unknown;
        var reason = $"phrase similarity={matches.PhraseScore:0.00} + keyword coverage={matches.Coverage:0.00}";
        var diagnostics = new[]
        {
            $"TopCenterStatus raw=\"{evidence}\"",
            $"normalized=\"{normalized}\"",
            $"best_match=\"{matches.Candidate.Status}\" score={matches.Score:0.00} category={category} reason=\"{reason}\""
        };
        return new SmartBpLifecycleStatusResult
        {
            IsRecognized = recognized,
            Status = status,
            Category = category,
            Score = matches.Score,
            Evidence = evidence,
            NormalizedText = normalized,
            HasDestinationEvidence = hasDestination,
            Diagnostics = diagnostics
        };
    }

    private static readonly (string Status, SmartBpLifecycleCategory Category, string[] Tokens)[] Candidates =
    [
        ("阵营选择中", SmartBpLifecycleCategory.CharacterBpActive, ["阵营", "选择"]),
        ("求生者天赋特质调整", SmartBpLifecycleCategory.SurvivorTalentAdjust, ["求生者", "天赋", "特质", "调整"]),
        ("监管者天赋特质调整", SmartBpLifecycleCategory.HunterTalentAdjust, ["监管者", "天赋", "特质", "调整"]),
        ("即将进入区域选择", SmartBpLifecycleCategory.TransitionToAreaSelection, ["即将", "进入", "区域", "选择"])
    ];

    private static double BestSubstringSimilarity(string text, string candidate)
    {
        text = SmartBpOcrTextResolver.NormalizeForMatch(text);
        candidate = SmartBpOcrTextResolver.NormalizeForMatch(candidate);
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(candidate)) return 0;
        if (text.Contains(candidate, StringComparison.Ordinal)) return 1;
        var best = 0d;
        var minLength = Math.Max(1, candidate.Length - 2);
        var maxLength = Math.Min(text.Length, candidate.Length + 2);
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
/// 使用纯 OCR 路径识别当前 BP 阶段和各角色区域业务状态。
/// </summary>
