using System.Text.Json;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>Matches noisy OCR role names against canonical Identity V character candidates.</summary>
public sealed partial class SmartBpOcrCandidateMatcher
{
    private readonly ISharedDataService _shared;
    private readonly ILogger<SmartBpOcrCandidateMatcher> _logger;
    private readonly Lazy<OcrAliasDocument> _aliases;
    private readonly string? _aliasPath;
    private readonly string? _resourceRoot;

    /// <summary>Initializes the OCR candidate matcher.</summary>
    /// <param name="shared">Shared candidate dictionaries.</param>
    /// <param name="logger">Logger.</param>
    public SmartBpOcrCandidateMatcher(
        ISharedDataService shared,
        ILogger<SmartBpOcrCandidateMatcher> logger)
    {
        _shared = shared;
        _logger = logger;
        _aliases = new(LoadAliases);
    }

    /// <summary>Initializes the OCR candidate matcher with module-local resource paths.</summary>
    /// <param name="shared">Shared candidate dictionaries.</param>
    /// <param name="storage">SmartBP module storage provider.</param>
    /// <param name="logger">Logger.</param>
    public SmartBpOcrCandidateMatcher(
        ISharedDataService shared,
        ISmartBpModuleStorageProvider storage,
        ILogger<SmartBpOcrCandidateMatcher> logger)
        : this(shared, logger)
    {
        _resourceRoot = Path.Combine(storage.ModuleRoot, "Resources");
    }

    internal SmartBpOcrCandidateMatcher(
        ISharedDataService shared,
        ILogger<SmartBpOcrCandidateMatcher> logger,
        string aliasPath) : this(shared, logger)
    {
        _aliasPath = aliasPath;
    }

    /// <summary>Matches one OCR text value to a canonical character.</summary>
    /// <param name="rawText">Raw OCR text.</param>
    /// <param name="camp">Expected camp.</param>
    /// <param name="slotIndex">Expected visual slot.</param>
    /// <returns>Normalized candidate result.</returns>
    public SmartBpNormalizedCharacter Match(string rawText, Camp camp, int slotIndex) =>
        Match(rawText, camp, slotIndex, null);

    /// <summary>Matches one provider-tagged OCR text value to a canonical character.</summary>
    /// <param name="rawText">Raw OCR text.</param>
    /// <param name="camp">Expected camp.</param>
    /// <param name="slotIndex">Expected visual slot.</param>
    /// <param name="provider">OCR provider name.</param>
    /// <returns>Normalized candidate result.</returns>
    public SmartBpNormalizedCharacter Match(string rawText, Camp camp, int slotIndex, string? provider)
    {
        var normalized = NormalizeForMatch(rawText);
        if (string.IsNullOrEmpty(normalized))
            return Unresolved(rawText, camp, slotIndex, normalized, "none", 0, provider, "empty text");

        var candidates = (camp == Camp.Hun ? _shared.HunCharaDict : _shared.SurCharaDict)
            .Values
            .Select(character => new Candidate(character.Name, NormalizeForMatch(character.Name)))
            .Where(candidate => candidate.Normalized.Length > 0)
            .DistinctBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();

        var exact = candidates.FirstOrDefault(candidate => candidate.Normalized == normalized);
        if (exact != null)
            return Resolved(rawText, exact.Name, camp, slotIndex, 1, "exact", normalized, provider, true);

        var aliasMatches = GetAliases(camp)
            .Where(pair => pair.Value.Any(alias => NormalizeForMatch(alias) == normalized))
            .Select(pair => candidates.FirstOrDefault(candidate => candidate.Name == pair.Key))
            .Where(candidate => candidate != null)
            .Cast<Candidate>()
            .DistinctBy(candidate => candidate.Name)
            .ToArray();
        if (aliasMatches.Length == 1)
            return Resolved(rawText, aliasMatches[0].Name, camp, slotIndex, .98, "alias", normalized, provider, true);
        if (aliasMatches.Length > 1)
            return Unresolved(rawText, camp, slotIndex, normalized, "alias", .98, provider, "ambiguous alias");

        var contained = candidates.Where(candidate =>
                normalized.Length >= 2 && candidate.Normalized.Length >= 2 &&
                (normalized.Contains(candidate.Normalized, StringComparison.Ordinal) ||
                 candidate.Normalized.Contains(normalized, StringComparison.Ordinal)))
            .OrderByDescending(candidate => Math.Min(normalized.Length, candidate.Normalized.Length))
            .ToArray();
        if (contained.Length == 1)
            return Resolved(rawText, contained[0].Name, camp, slotIndex, .95, "contains", normalized, provider, true);
        if (contained.Length > 1 && contained[0].Normalized.Length > contained[1].Normalized.Length)
            return Resolved(rawText, contained[0].Name, camp, slotIndex, .95, "contains", normalized, provider, true);

        var scored = candidates
            .Select(candidate => new { Candidate = candidate, Score = Similarity(normalized, candidate.Normalized) })
            .OrderByDescending(item => item.Score)
            .ToArray();
        if (scored.Length > 0)
        {
            var best = scored[0];
            var next = scored.Length > 1 ? scored[1].Score : 0;
            if (best.Score >= .82 && best.Score - next >= .15)
                return Resolved(rawText, best.Candidate.Name, camp, slotIndex, Math.Min(best.Score, .89), "fuzzy", normalized, provider, false);

            if (camp == Camp.Hun && slotIndex == 0 && normalized.Length == 1)
            {
                var oneCharacter = candidates.Where(candidate => candidate.Normalized.Contains(normalized, StringComparison.Ordinal)).ToArray();
                if (oneCharacter.Length == 1)
                    return Resolved(rawText, oneCharacter[0].Name, camp, slotIndex, .75, "fuzzy-one-character", normalized, provider, false);
            }
        }

        return Unresolved(rawText, camp, slotIndex, normalized, "none", scored.FirstOrDefault()?.Score ?? 0, provider, "no unique candidate");
    }

    /// <summary>Normalizes text for OCR candidate comparison.</summary>
    /// <param name="value">Input text.</param>
    /// <returns>Normalized comparison text.</returns>
    public static string NormalizeForMatch(string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim();
        normalized = SmartBpOcrTextResolver.StripDecorativeQuotes(normalized);
        return NonWordRegex().Replace(normalized, string.Empty).ToUpperInvariant();
    }

    private SmartBpNormalizedCharacter Resolved(
        string raw, string candidate, Camp camp, int slot, double score, string mode, string normalized, string? provider, bool isAutoApplySafe)
    {
        var diagnostic = Diagnostic(raw, normalized, candidate, mode, score, provider);
        _logger.LogDebug("{Diagnostic}", diagnostic);
        return new(raw, candidate, candidate, camp, slot, score, [diagnostic], mode, isAutoApplySafe, diagnostic);
    }

    private SmartBpNormalizedCharacter Unresolved(
        string raw, Camp camp, int slot, string normalized, string mode, double score, string? provider, string reason)
    {
        var diagnostic = $"{Diagnostic(raw, normalized, "unresolved", mode, score, provider)}; reason={reason}";
        _logger.LogDebug("{Diagnostic}", diagnostic);
        return new(raw, null, null, camp, slot, Math.Min(score, .89), [diagnostic], mode, false, reason);
    }

    private static string Diagnostic(string raw, string normalized, string candidate, string mode, double score, string? provider) =>
        $"raw={raw}; normalized={normalized}; candidate={candidate}; matchMode={mode}; score={score:0.00}; provider={provider ?? "unknown"}";

    private IReadOnlyDictionary<string, string[]> GetAliases(Camp camp) =>
        camp == Camp.Hun ? _aliases.Value.Hunter : _aliases.Value.Survivor;

    private OcrAliasDocument LoadAliases()
    {
        var path = _aliasPath ?? Path.Combine(_resourceRoot ?? Path.Combine(AppContext.BaseDirectory, "Resources"), "SmartBp", "OcrCharacterAliases.json");
        try
        {
            string? json = File.Exists(path) ? File.ReadAllText(path) : null;
            return json == null
                ? new()
                : JsonSerializer.Deserialize<OcrAliasDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load OCR character aliases from {Path}.", path);
            return new();
        }
    }

    private static double Similarity(string left, string right)
    {
        if (left == right) return 1;
        if (left.Length == 0 || right.Length == 0) return 0;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            previous = current;
        }
        return 1 - previous[^1] / (double)Math.Max(left.Length, right.Length);
    }

    private sealed record Candidate(string Name, string Normalized);
    private sealed class OcrAliasDocument
    {
        public Dictionary<string, string[]> Hunter { get; set; } = [];
        public Dictionary<string, string[]> Survivor { get; set; } = [];
    }

    [GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordRegex();
}
