#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器的ScoreGlobal逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private static void ApplyScoreGlobalAggregateGeometry(
        string window,
        string canvas,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!string.Equals(window, "ScoreGlobalWindow", StringComparison.Ordinal)
            || !string.Equals(canvas, "BaseCanvas", StringComparison.Ordinal))
        {
            return;
        }

        ApplyScoreGlobalRowGeometry(
            "Home",
            "HomeGlobalScoreRow",
            config,
            legacyPositions,
            consumedControls,
            messages);
        ApplyScoreGlobalRowGeometry(
            "Away",
            "AwayGlobalScoreRow",
            config,
            legacyPositions,
            consumedControls,
            messages);
    }

    private static void ApplyScoreGlobalRowGeometry(
        string teamPrefix,
        string targetControlName,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!config.Controls.TryGetValue(targetControlName, out var control)
            || control is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        var cells = legacyPositions
            .Select(item => TryParseScoreGlobalCell(item.Key, out var team, out var game, out var half, out var isOvertime)
                ? new ScoreGlobalCell(item.Key, team, game, half, isOvertime, item.Value)
                : null)
            .Where(cell => cell is not null
                           && string.Equals(cell.Team, teamPrefix, StringComparison.Ordinal))
            .Cast<ScoreGlobalCell>()
            .ToArray();
        if (cells.Length == 0)
        {
            return;
        }

        foreach (var cell in cells)
        {
            consumedControls.Add(cell.ControlName);
        }

        if (cells.Any(cell => cell.IsOvertime))
        {
            messages.Add(Info(CodeOvertimeScoreCellsAggregated));
        }

        var firstHalfByGame = cells
            .Where(cell => !cell.IsOvertime && cell.Half == "FirstHalf" && cell.Info.Left.HasValue)
            .GroupBy(cell => cell.Game)
            .ToDictionary(group => group.Key, group => group.First().Info);
        var secondHalfByGame = cells
            .Where(cell => !cell.IsOvertime && cell.Half == "SecondHalf" && cell.Info.Left.HasValue)
            .GroupBy(cell => cell.Game)
            .ToDictionary(group => group.Key, group => group.First().Info);

        var gameOneFirstHalf = firstHalfByGame.GetValueOrDefault(1);
        var left = gameOneFirstHalf?.Left
                   ?? cells.Select(cell => cell.Info.Left).Where(value => value.HasValue).Min();
        if (left.HasValue)
        {
            row.Left = FrontedLayoutNumberNormalizer.Normalize(left.Value);
        }

        var top = gameOneFirstHalf?.Top
                  ?? GetMedian(cells.Select(cell => cell.Info.Top).Where(value => value.HasValue).Select(value => value!.Value))
                  ?? cells.Select(cell => cell.Info.Top).FirstOrDefault(value => value.HasValue);
        if (top.HasValue)
        {
            row.Top = FrontedLayoutNumberNormalizer.Normalize(top.Value);
        }

        var halfGaps = firstHalfByGame
            .Where(item => secondHalfByGame.TryGetValue(item.Key, out var secondHalf)
                           && item.Value.Left.HasValue
                           && secondHalf.Left.HasValue)
            .Select(item => secondHalfByGame[item.Key].Left!.Value - item.Value.Left!.Value)
            .Where(gap => gap > 0)
            .ToArray();
        var halfGap = GetMedian(halfGaps);
        if (halfGap.HasValue)
        {
#pragma warning disable CS0618
            row.HalfGameGap = FrontedLayoutNumberNormalizer.Normalize(halfGap.Value);
#pragma warning restore CS0618
        }

        var gameLefts = firstHalfByGame
            .Where(item => item.Value.Left.HasValue)
            .OrderBy(item => item.Key)
            .Select(item => new { Game = item.Key, Left = item.Value.Left!.Value })
            .ToArray();
        var majorGaps = gameLefts
            .Zip(gameLefts.Skip(1), (previous, next) => next.Game == previous.Game + 1
                ? next.Left - previous.Left
                : (double?)null)
            .Where(gap => gap.HasValue && gap.Value > 0)
            .Select(gap => gap!.Value)
            .ToArray();
        var majorGap = GetMedian(majorGaps);
        if (majorGap.HasValue)
        {
#pragma warning disable CS0618
            row.MajorGameGap = FrontedLayoutNumberNormalizer.Normalize(majorGap.Value);
#pragma warning restore CS0618
        }

        MigrateLegacyScoreCellsToRowCells(row, cells);

        var approximate = IsIrregular(halfGaps) || IsIrregular(majorGaps);
        messages.Add(Info(CodeGlobalScoreCellsAggregated,
            Args(new { Team = teamPrefix, TargetName = targetControlName })));
        if (approximate)
        {
            messages.Add(Info(CodeIrregularCellSpacingApproximated,
                Args(new { Team = teamPrefix, TargetName = targetControlName })));
        }
    }

    private static void MigrateLegacyScoreCellsToRowCells(
        GlobalScoreRowControlConfig row,
        IReadOnlyList<ScoreGlobalCell> legacyCells)
    {
        var existingCells = row.Cells.ToDictionary(
            cell => (cell.GameNumber, cell.GameKind, cell.HalfKind));
        var migrated = new List<GlobalScoreCellConfig>();

        foreach (var legacy in legacyCells
                     .Where(cell => cell.Info.Left.HasValue || cell.Info.Top.HasValue || cell.Info.Width.HasValue || cell.Info.Height.HasValue)
                     .OrderBy(cell => cell.Game)
                     .ThenBy(cell => cell.IsOvertime)
                     .ThenBy(cell => cell.Half == "SecondHalf" ? 1 : 0))
        {
            var gameKind = legacy.IsOvertime ? ScoreGameKind.Overtime : ScoreGameKind.Normal;
            var halfKind = legacy.Half == "SecondHalf" ? ScoreHalfKind.SecondHalf : ScoreHalfKind.FirstHalf;
            var key = (legacy.Game, gameKind, halfKind);
            if (!existingCells.TryGetValue(key, out var cell))
            {
                cell = new GlobalScoreCellConfig
                {
                    Id = $"Game{legacy.Game}{(legacy.IsOvertime ? "Overtime" : string.Empty)}{halfKind}",
                    GameNumber = legacy.Game,
                    GameKind = gameKind,
                    HalfKind = halfKind,
                    Width = 75,
                    Height = 32
                };
            }

            if (legacy.Info.Left.HasValue)
            {
                cell.X = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Left.Value - row.Left);
            }

            if (legacy.Info.Top.HasValue)
            {
                cell.Y = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Top.Value - row.Top);
            }

            if (legacy.Info.Width.HasValue)
            {
                cell.Width = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Width.Value);
            }

            if (legacy.Info.Height.HasValue)
            {
                cell.Height = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Height.Value);
            }

            migrated.Add(cell);
        }

        if (migrated.Count > 0)
        {
            row.Cells = migrated;
            row.Width = Math.Max(row.Width ?? 0D, migrated.Max(cell => cell.X + cell.Width));
            row.Height = Math.Max(row.Height ?? 0D, migrated.Max(cell => cell.Y + cell.Height));
        }
    }

    private static bool TryParseScoreGlobalCell(
        string controlName,
        out string team,
        out int game,
        out string half,
        out bool isOvertime)
    {
        if (!LegacyScoreGlobalCells.TryGetValue(controlName, out var blueprint))
        {
            team = string.Empty;
            game = 0;
            half = string.Empty;
            isOvertime = false;
            return false;
        }

        team = blueprint.Team;
        game = blueprint.Game;
        half = blueprint.Half;
        isOvertime = blueprint.IsOvertime;
        return true;
    }

    private static void ConsumeExplicitFoldedGeometry(
        string window,
        string canvas,
        IReadOnlyList<LegacyControlBlueprint> blueprints,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        foreach (var blueprint in blueprints.Where(blueprint => blueprint.Status == LegacyControlBlueprintStatus.Folded))
        {
            if (!legacyPositions.ContainsKey(blueprint.LegacyName))
            {
                continue;
            }

            consumedControls.Add(blueprint.LegacyName);
            messages.Add(Info(CodeFoldedControlConsumed,
                Args(new { SourceWindow = window, SourceCanvas = canvas, LegacyName = blueprint.LegacyName, TargetName = blueprint.TargetName })));
            messages.Add(Info(CodeLockOverlayGeometryConsumed,
                Args(new { LegacyName = blueprint.LegacyName, TargetName = blueprint.TargetName })));
            if (string.IsNullOrWhiteSpace(blueprint.TargetName)
                || !config.Controls.TryGetValue(blueprint.TargetName, out var target))
            {
                messages.Add(Info(CodeFoldedControlNoTarget,
                    Args(new { SourceWindow = window, SourceCanvas = canvas, LegacyName = blueprint.LegacyName })));
                continue;
            }

            if (target is ImageFrontedControlConfig image)
            {
                ApplyImageSpecialProperties(image, blueprint);
            }

            messages.Add(Info(CodeFoldedGeometryNotRepresentable));
        }
    }

    private static void ApplyGeometry(FrontedControlConfigBase control, ElementInfo legacy)
    {
        if (legacy.Left.HasValue)
        {
            control.Left = FrontedLayoutNumberNormalizer.Normalize(legacy.Left.Value);
        }

        if (legacy.Top.HasValue)
        {
            control.Top = FrontedLayoutNumberNormalizer.Normalize(legacy.Top.Value);
        }

        if (legacy.Width.HasValue)
        {
            control.Width = FrontedLayoutNumberNormalizer.Normalize(legacy.Width.Value);
        }

        if (legacy.Height.HasValue)
        {
            control.Height = FrontedLayoutNumberNormalizer.Normalize(legacy.Height.Value);
        }
    }

    private static void AddBoundsDiagnostics(
        LegacyLayoutMapping mapping,
        IEnumerable<ElementInfo> legacyPositions,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!string.Equals(mapping.SourceWindow, "WidgetsWindow", StringComparison.Ordinal)
            || !string.Equals(mapping.SourceCanvas, "BpOverViewCanvas", StringComparison.Ordinal)
            || !mapping.FixedCanvasWidth.HasValue
            || !mapping.FixedCanvasHeight.HasValue)
        {
            return;
        }

        var bounds = PaintedBounds.From(legacyPositions);
        if (bounds is null)
        {
            return;
        }

        const double tolerance = 0.01D;
        if (bounds.Value.MinX < -tolerance
            || bounds.Value.MinY < -tolerance
            || bounds.Value.MaxX > mapping.FixedCanvasWidth.Value + tolerance
            || bounds.Value.MaxY > mapping.FixedCanvasHeight.Value + tolerance)
        {
            messages.Add(Warning(CodeBpOverviewOutOfBounds));
        }
    }

    private static double? GetMedian(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static bool IsIrregular(IReadOnlyList<double> values)
    {
        return values.Count > 1
               && values.Max() - values.Min() > 1;
    }

}

#pragma warning restore CS1591
