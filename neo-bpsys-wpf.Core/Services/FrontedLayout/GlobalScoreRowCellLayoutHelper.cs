using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.ScoreSystem;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Design-time helper for materializing and arranging GlobalScoreRow child cells.
/// </summary>
public static class GlobalScoreRowCellLayoutHelper
{
    public const double DefaultCellWidth = 75D;
    public const double DefaultCellHeight = 32D;

    private static readonly IReadOnlyList<CellDefinition> CompleteTemplate =
    [
        new("Game1FirstHalf", 1, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
        new("Game1SecondHalf", 1, ScoreGameKind.Normal, ScoreHalfKind.SecondHalf),
        new("Game2FirstHalf", 2, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
        new("Game2SecondHalf", 2, ScoreGameKind.Normal, ScoreHalfKind.SecondHalf),
        new("Game3FirstHalf", 3, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
        new("Game3SecondHalf", 3, ScoreGameKind.Normal, ScoreHalfKind.SecondHalf),
        new("Game4FirstHalf", 4, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
        new("Game4SecondHalf", 4, ScoreGameKind.Normal, ScoreHalfKind.SecondHalf),
        new("Game5FirstHalf", 5, ScoreGameKind.Normal, ScoreHalfKind.FirstHalf),
        new("Game5SecondHalf", 5, ScoreGameKind.Normal, ScoreHalfKind.SecondHalf),
        new("Game3OvertimeFirstHalf", 3, ScoreGameKind.Overtime, ScoreHalfKind.FirstHalf),
        new("Game3OvertimeSecondHalf", 3, ScoreGameKind.Overtime, ScoreHalfKind.SecondHalf),
        new("Game5OvertimeFirstHalf", 5, ScoreGameKind.Overtime, ScoreHalfKind.FirstHalf),
        new("Game5OvertimeSecondHalf", 5, ScoreGameKind.Overtime, ScoreHalfKind.SecondHalf)
    ];

    public static IReadOnlyList<string> CompleteCellIds => CompleteTemplate.Select(cell => cell.Id).ToArray();

    public static List<GlobalScoreCellConfig> CreateCompleteCellTemplate(
        double majorGameGap = 180D,
        double halfGameGap = 90D,
        bool isBo3Mode = false)
    {
        var row = new GlobalScoreRowControlConfig
        {
            MajorGameGap = majorGameGap,
            HalfGameGap = halfGameGap
        };

        foreach (var definition in CompleteTemplate)
        {
            row.Cells.Add(CreateCell(definition, majorGameGap, halfGameGap, isBo3Mode));
        }

        if (isBo3Mode)
        {
            ApplyBo3VisibilityTemplate(row);
            AutoArrangeBySpacing(row, isBo3Mode: true);
        }
        else
        {
            ApplyBo5VisibilityTemplate(row);
            AutoArrangeBySpacing(row, isBo3Mode: false);
        }

        return row.Cells;
    }

    public static bool EnsureCompleteCells(GlobalScoreRowControlConfig row, bool isBo3Mode = false)
    {
        var changed = false;
        var byId = row.Cells
            .Where(cell => !string.IsNullOrWhiteSpace(cell.Id))
            .GroupBy(cell => cell.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var definition in CompleteTemplate)
        {
            if (byId.ContainsKey(definition.Id))
            {
                continue;
            }

            var matching = row.Cells.FirstOrDefault(cell =>
                cell.GameNumber == definition.GameNumber
                && cell.GameKind == definition.GameKind
                && cell.HalfKind == definition.HalfKind);
            if (matching is not null && string.IsNullOrWhiteSpace(matching.Id))
            {
                matching.Id = definition.Id;
                byId[definition.Id] = matching;
                changed = true;
                continue;
            }

            row.Cells.Add(CreateCell(definition, row.MajorGameGap, row.HalfGameGap, isBo3Mode));
            changed = true;
        }

        if (changed)
        {
            row.Cells = row.Cells
                .OrderBy(cell => GetTemplateIndex(cell.Id))
                .ThenBy(cell => cell.Id, StringComparer.Ordinal)
                .ToList();
        }

        return changed;
    }

    public static void ApplyBo3VisibilityTemplate(GlobalScoreRowControlConfig row)
    {
        EnsureCompleteCells(row, isBo3Mode: true);
        foreach (var cell in row.Cells)
        {
            cell.Visibility = IsBo3Visible(cell)
                ? FrontedControlVisibility.Visible
                : FrontedControlVisibility.Collapsed;
        }
    }

    public static void ApplyBo5VisibilityTemplate(GlobalScoreRowControlConfig row)
    {
        EnsureCompleteCells(row, isBo3Mode: false);
        foreach (var cell in row.Cells)
        {
            cell.Visibility = IsBo5Visible(cell)
                ? FrontedControlVisibility.Visible
                : FrontedControlVisibility.Collapsed;
        }
    }

    public static void AutoArrangeBySpacing(GlobalScoreRowControlConfig row, bool isBo3Mode)
    {
        EnsureCompleteCells(row, isBo3Mode);
        var order = isBo3Mode
            ? new[]
            {
                "Game1FirstHalf", "Game1SecondHalf",
                "Game2FirstHalf", "Game2SecondHalf",
                "Game3FirstHalf", "Game3SecondHalf",
                "Game3OvertimeFirstHalf", "Game3OvertimeSecondHalf"
            }
            : new[]
            {
                "Game1FirstHalf", "Game1SecondHalf",
                "Game2FirstHalf", "Game2SecondHalf",
                "Game3FirstHalf", "Game3SecondHalf",
                "Game4FirstHalf", "Game4SecondHalf",
                "Game5FirstHalf", "Game5SecondHalf",
                "Game5OvertimeFirstHalf", "Game5OvertimeSecondHalf"
            };

        for (var index = 0; index < order.Length; index += 2)
        {
            var gameIndex = index / 2;
            SetPosition(row, order[index], gameIndex, halfIndex: 0);
            SetPosition(row, order[index + 1], gameIndex, halfIndex: 1);
        }
    }

    private static void SetPosition(GlobalScoreRowControlConfig row, string id, int gameIndex, int halfIndex)
    {
        var cell = row.Cells.FirstOrDefault(cell => string.Equals(cell.Id, id, StringComparison.Ordinal));
        if (cell is null)
        {
            return;
        }

        cell.X = gameIndex * row.MajorGameGap + halfIndex * row.HalfGameGap;
        cell.Y = 0D;
    }

    private static GlobalScoreCellConfig CreateCell(
        CellDefinition definition,
        double majorGameGap,
        double halfGameGap,
        bool isBo3Mode)
    {
        var gameIndex = ResolveInitialGameIndex(definition, isBo3Mode);
        return new GlobalScoreCellConfig
        {
            Id = definition.Id,
            GameNumber = definition.GameNumber,
            GameKind = definition.GameKind,
            HalfKind = definition.HalfKind,
            X = gameIndex * majorGameGap + (definition.HalfKind == ScoreHalfKind.SecondHalf ? halfGameGap : 0D),
            Y = 0D,
            Width = DefaultCellWidth,
            Height = DefaultCellHeight,
            Visibility = (isBo3Mode ? IsBo3Visible(definition) : IsBo5Visible(definition))
                ? FrontedControlVisibility.Visible
                : FrontedControlVisibility.Collapsed
        };
    }

    private static int ResolveInitialGameIndex(CellDefinition definition, bool isBo3Mode)
    {
        if (isBo3Mode)
        {
            return definition.GameKind == ScoreGameKind.Overtime && definition.GameNumber == 3
                ? 3
                : Math.Max(0, definition.GameNumber - 1);
        }

        return definition.GameKind == ScoreGameKind.Overtime && definition.GameNumber == 5
            ? 5
            : Math.Max(0, definition.GameNumber - 1);
    }

    private static bool IsBo3Visible(GlobalScoreCellConfig cell) =>
        IsBo3Visible(new CellDefinition(cell.Id, cell.GameNumber, cell.GameKind, cell.HalfKind));

    private static bool IsBo3Visible(CellDefinition cell) =>
        cell.GameKind == ScoreGameKind.Normal && cell.GameNumber is >= 1 and <= 3
        || cell.GameKind == ScoreGameKind.Overtime && cell.GameNumber == 3;

    private static bool IsBo5Visible(GlobalScoreCellConfig cell) =>
        IsBo5Visible(new CellDefinition(cell.Id, cell.GameNumber, cell.GameKind, cell.HalfKind));

    private static bool IsBo5Visible(CellDefinition cell) =>
        cell.GameKind == ScoreGameKind.Normal && cell.GameNumber is >= 1 and <= 5
        || cell.GameKind == ScoreGameKind.Overtime && cell.GameNumber == 5;

    private static int GetTemplateIndex(string id)
    {
        for (var index = 0; index < CompleteTemplate.Count; index++)
        {
            if (string.Equals(CompleteTemplate[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private readonly record struct CellDefinition(
        string Id,
        int GameNumber,
        ScoreGameKind GameKind,
        ScoreHalfKind HalfKind);
}
