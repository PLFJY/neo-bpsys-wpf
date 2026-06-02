#pragma warning disable CS1591

using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class LegacyFrontedControlNameMapper
{
    private static readonly Dictionary<string, string> ScoreGlobalBaseCanvasAliases =
        new(StringComparer.Ordinal)
        {
            ["MainTeamName"] = "HomeTeamName",
            ["MainScoreTotal"] = "HomeScoreTotal",
            ["AwayTeamName"] = "AwayTeamName",
            ["AwayScoreTotal"] = "AwayScoreTotal"
        };

    private static readonly Regex NonNameChars = new("[^A-Za-z0-9]+", RegexOptions.Compiled);

    public static bool TryResolve(
        string window,
        string canvas,
        string legacyName,
        IReadOnlyDictionary<string, FrontedControlConfigBase> controls,
        out string controlName,
        out bool usedFuzzyMatch)
    {
        usedFuzzyMatch = false;
        if (controls.ContainsKey(legacyName))
        {
            controlName = legacyName;
            return true;
        }

        if (TryGetAlias(window, canvas, legacyName, out var alias)
            && controls.ContainsKey(alias))
        {
            controlName = alias;
            return true;
        }

        var normalizedLegacyName = Normalize(legacyName);
        var fuzzy = controls.Keys.FirstOrDefault(name =>
            string.Equals(Normalize(name), normalizedLegacyName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(fuzzy))
        {
            controlName = fuzzy;
            usedFuzzyMatch = true;
            return true;
        }

        controlName = string.Empty;
        return false;
    }

    public static IReadOnlyList<string> GetClosestCandidates(
        string legacyName,
        IEnumerable<string> controlNames,
        int maxCount = 3)
    {
        var normalizedLegacyName = Normalize(legacyName);
        return controlNames
            .Select(name => new
            {
                Name = name,
                Distance = GetLevenshteinDistance(normalizedLegacyName, Normalize(name))
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .Take(maxCount)
            .Select(item => item.Name)
            .ToArray();
    }

    public static bool IsScoreGlobalAggregateCell(string window, string canvas, string legacyName)
    {
        return IsScoreGlobalBaseCanvas(window, canvas)
               && TryParseScoreCellName(legacyName, out _, out _, out _);
    }

    private static bool TryGetAlias(
        string window,
        string canvas,
        string legacyName,
        out string alias)
    {
        if (IsScoreGlobalBaseCanvas(window, canvas))
        {
            if (ScoreGlobalBaseCanvasAliases.TryGetValue(legacyName, out alias!))
            {
                return true;
            }

            if (legacyName.StartsWith("Main", StringComparison.Ordinal)
                && legacyName.Length > "Main".Length)
            {
                alias = "Home" + legacyName["Main".Length..];
                return true;
            }
        }

        alias = string.Empty;
        return false;
    }

    private static bool IsScoreGlobalBaseCanvas(string window, string canvas)
    {
        return string.Equals(window, "ScoreGlobalWindow", StringComparison.Ordinal)
               && string.Equals(canvas, "BaseCanvas", StringComparison.Ordinal);
    }

    private static bool TryParseScoreCellName(
        string legacyName,
        out string team,
        out int game,
        out string half)
    {
        team = string.Empty;
        game = 0;
        half = string.Empty;

        var match = Regex.Match(
            legacyName,
            @"^(Home|Away)TeamGame(?<game>\d+)(?<overtime>Overtime)?(?<half>FirstHalf|SecondHalf)$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        team = match.Groups[1].Value;
        game = int.Parse(match.Groups["game"].Value);
        half = match.Groups["half"].Value;
        return true;
    }

    private static string Normalize(string name)
    {
        return NonNameChars.Replace(name, string.Empty);
    }

    private static int GetLevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var i = 0; i <= right.Length; i++)
        {
            previous[i] = i;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}

#pragma warning restore CS1591
