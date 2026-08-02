using neo_bpsys_wpf.Core.Abstractions.Services;
using OpenCvSharp;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 根据完整捕获帧 OCR 的文本边界框重建赛后数据表格。
/// </summary>
internal static partial class GameDataTableOcrParser
{
    /// <summary>
    /// 从完整捕获帧 OCR 文本中重建赛后数据行与五个数据列。
    /// </summary>
    /// <param name="lines">以完整捕获帧为坐标系的 OCR 文本行。</param>
    /// <returns>包含解析行与诊断信息的结果。</returns>
    internal static GameDataTableParseResult Parse(IReadOnlyList<OcrTextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var diagnostics = new List<string>();
        var names = lines
            .Select(line => new { Line = line, Match = PlayerAndCharacterRegex().Match(NormalizeParentheses(line.Text)) })
            .Where(item => item.Match.Success)
            .Select(item => new NameCandidate(
                item.Line.CenterY,
                item.Line.BoundingBox.Right,
                item.Match.Groups[1].Value.Trim(),
                item.Match.Groups[2].Value.Trim(),
                item.Line.Text))
            .Where(item => !string.IsNullOrWhiteSpace(item.PlayerName) && !string.IsNullOrWhiteSpace(item.CharacterName))
            .OrderBy(item => item.CenterY)
            .Take(5)
            .ToArray();
        diagnostics.Add($"name candidates={names.Length}");

        var values = names
            .Select(_ => Enumerable.Repeat(string.Empty, 5).ToArray())
            .ToArray();
        var evidence = names.Select(_ => new bool[5]).ToArray();
        if (names.Length == 0)
            return new([], diagnostics, [], null);

        // 名称文本右边界定义左侧资料区的终点；等级/天赋数字位于其左侧，不参与统计列聚类。
        var dataStartX = names.Max(name => name.RightX);
        foreach (var line in lines.Where(line => line.CenterX <= dataStartX && DigitsRegex().IsMatch(line.Text)))
        {
            diagnostics.Add($"ignored name-column text raw=[{line.Text}] x={line.CenterX:0.#} y={line.CenterY:0.#}");
        }

        var numeric = lines
            .Where(line => line.CenterX > dataStartX)
            .Select(line => new NumericCandidate(line, DigitsRegex().Match(line.Text).Value))
            .Where(item => !string.IsNullOrEmpty(item.Digits) || IsEmptyCellMarker(item.Line.Text))
            .ToArray();
        if (numeric.Length == 0)
        {
            diagnostics.Add("no numeric table candidates to the right of player/character text");
            return BuildRows(names, values, evidence, diagnostics, [], null);
        }

        var left = numeric.Min(item => item.Line.CenterX);
        var right = numeric.Max(item => item.Line.CenterX);
        var step = right > left ? (right - left) / 4d : 1d;
        var expectedCenters = Enumerable.Range(0, 5)
            .Select(column => left + step * column)
            .ToArray();
        var expectedRowCenters = names
            .Select((name, rowIndex) => numeric
                .Where(item => FindNearestRow(names, item.Line.CenterY) == rowIndex)
                .Select(item => item.Line.CenterY)
                .DefaultIfEmpty(name.CenterY + 24)
                .Average())
            .ToArray();
        diagnostics.Add($"data grid inferred left={left:0.#}; right={right:0.#}; step={step:0.#}; expected_centers=[{string.Join(",", expectedCenters.Select(value => value.ToString("0.#")))}]");

        foreach (var item in numeric)
        {
            var rowIndex = FindNearestRow(names, item.Line.CenterY);
            var columnIndex = step <= 1 ? 0 : Math.Clamp((int)Math.Round((item.Line.CenterX - left) / step), 0, 4);
            if (rowIndex < 0)
                continue;

            if (string.IsNullOrEmpty(item.Digits))
            {
                evidence[rowIndex][columnIndex] = true;
                diagnostics.Add($"assigned empty data row={rowIndex} column={columnIndex} x={item.Line.CenterX:0.#} y={item.Line.CenterY:0.#}");
                continue;
            }

            if (!string.IsNullOrEmpty(values[rowIndex][columnIndex]))
            {
                diagnostics.Add($"ignored duplicate data row={rowIndex} column={columnIndex} value=[{item.Digits}]");
                continue;
            }

            values[rowIndex][columnIndex] = item.Digits;
            evidence[rowIndex][columnIndex] = true;
            diagnostics.Add($"assigned data row={rowIndex} column={columnIndex} value=[{item.Digits}] x={item.Line.CenterX:0.#} y={item.Line.CenterY:0.#}");
        }

        var missingCells = new List<GameDataTableMissingCell>();
        for (var rowIndex = 0; rowIndex < names.Length; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < values[rowIndex].Length; columnIndex++)
            {
                if (!string.IsNullOrEmpty(values[rowIndex][columnIndex]) || evidence[rowIndex][columnIndex])
                    continue;

                var expectedX = expectedCenters[columnIndex];
                var nearest = numeric
                    .Where(item => FindNearestRow(names, item.Line.CenterY) == rowIndex)
                    .OrderBy(item => Math.Abs(item.Line.CenterX - expectedX))
                    .FirstOrDefault();
                diagnostics.Add(nearest == null
                    ? $"missing data row={rowIndex} column={columnIndex} expected_x={expectedX:0.#} nearest_candidate=none"
                    : $"missing data row={rowIndex} column={columnIndex} expected_x={expectedX:0.#} nearest_candidate=[{nearest.Line.Text}] x={nearest.Line.CenterX:0.#} distance={Math.Abs(nearest.Line.CenterX - expectedX):0.#}");
                missingCells.Add(new(rowIndex, columnIndex, expectedX, expectedRowCenters[rowIndex]));
            }
        }

        var layout = BuildLayout(expectedCenters, expectedRowCenters, step, diagnostics);
        return BuildRows(names, values, evidence, diagnostics, missingCells, layout);
    }

    /// <summary>
    /// 根据本次 OCR 推断的列中心、行中心、列宽与行高，构建数字网格边界。
    /// 该方法不复制列中心推断算法，仅消费 <see cref="Parse"/> 已计算出的几何信息。
    /// </summary>
    /// <param name="columnCenters">五个数据列中心 X 坐标。</param>
    /// <param name="rowCenters">数据行中心 Y 坐标。</param>
    /// <param name="step">列宽估算。</param>
    /// <param name="diagnostics">诊断信息输出。</param>
    /// <returns>表格几何布局；行或列信息不足时返回 <see langword="null"/>。</returns>
    private static GameDataTableLayout? BuildLayout(
        double[] columnCenters,
        double[] rowCenters,
        double step,
        List<string> diagnostics)
    {
        if (columnCenters.Length == 0 || rowCenters.Length == 0)
            return null;

        var columnWidth = step > 1 ? step : 1d;
        var rowHeight = rowCenters.Length >= 2
            ? rowCenters.Zip(rowCenters.Skip(1), (previous, current) => current - previous).Average()
            : 30d;
        if (rowHeight <= 0)
            rowHeight = 30d;

        var gridLeft = columnCenters.Min() - columnWidth * 0.5;
        var gridRight = columnCenters.Max() + columnWidth * 0.5;
        var gridTop = rowCenters.Min() - rowHeight * 0.5;
        var gridBottom = rowCenters.Max() + rowHeight * 0.5;

        var marginX = columnWidth * 0.25;
        var marginY = rowHeight * 0.25;
        var left = (int)Math.Floor(gridLeft - marginX);
        var top = (int)Math.Floor(gridTop - marginY);
        var width = Math.Max(1, (int)Math.Ceiling(gridRight - gridLeft + 2 * marginX));
        var height = Math.Max(1, (int)Math.Ceiling(gridBottom - gridTop + 2 * marginY));
        var bounds = new Rect(left, top, width, height);

        diagnostics.Add($"layout inferred columns=[{string.Join(",", columnCenters.Select(value => value.ToString("0.#")))}]; rows=[{string.Join(",", rowCenters.Select(value => value.ToString("0.#")))}]; row_height={rowHeight:0.#}; column_width={columnWidth:0.#}; grid_bounds={bounds}.");
        return new GameDataTableLayout(
            rowCenters,
            columnCenters,
            bounds,
            rowHeight,
            columnWidth);
    }

    private static GameDataTableParseResult BuildRows(
        IReadOnlyList<NameCandidate> names,
        IReadOnlyList<string[]> values,
        IReadOnlyList<bool[]> evidence,
        List<string> diagnostics,
        IReadOnlyList<GameDataTableMissingCell> missingCells,
        GameDataTableLayout? layout)
    {
        var rows = names.Select((name, index) => new GameDataTableRow(
            index,
            name.PlayerName,
            name.CharacterName,
            values[index],
            evidence[index].All(value => value),
            name.CenterY,
            name.RawText)).ToArray();
        diagnostics.AddRange(rows.Select(row =>
            $"parsed row={row.RowIndex} y={row.CenterY:0.#} complete={row.HasAllDataColumns} player=[{row.PlayerName}] character=[{row.CharacterName}] values=[{string.Join(",", row.Values)}]"));
        return new(rows, diagnostics, missingCells, layout);
    }

    private static int FindNearestRow(IReadOnlyList<NameCandidate> names, double y) => names
        .Select((candidate, index) => new { Index = index, Distance = Math.Abs(candidate.CenterY - y) })
        .OrderBy(item => item.Distance)
        .Select(item => item.Index)
        .FirstOrDefault(-1);

    private static string NormalizeParentheses(string text) => text.Replace('（', '(').Replace('）', ')').Trim();

    [GeneratedRegex(@"^([^()]+?)\(([^()]+)\)$")]
    private static partial Regex PlayerAndCharacterRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    private static bool IsEmptyCellMarker(string text) => text.Trim() is "-" or "—" or "–";

    private sealed record NameCandidate(double CenterY, double RightX, string PlayerName, string CharacterName, string RawText);
    private sealed record NumericCandidate(OcrTextLine Line, string Digits);
}

/// <summary>
/// 一条重建后的赛后数据行。缺少 OCR 文本的数据列保留为空，以延续赛后数据的空值语义。
/// </summary>
internal sealed record GameDataTableRow(int RowIndex, string PlayerName, string CharacterName, IReadOnlyList<string> Values, bool HasAllDataColumns, double CenterY, string RawNameText);

/// <summary>
/// 一处未被整表 OCR 识别到的数据格及其推断位置。
/// </summary>
/// <param name="RowIndex">数据行索引。</param>
/// <param name="ColumnIndex">数据列索引。</param>
/// <param name="ExpectedCenterX">数据格推断中心 X 坐标。</param>
/// <param name="ExpectedCenterY">数据格推断中心 Y 坐标。</param>
internal sealed record GameDataTableMissingCell(int RowIndex, int ColumnIndex, double ExpectedCenterX, double ExpectedCenterY);

/// <summary>
/// 整表 OCR 解析结果。
/// </summary>
/// <param name="Rows">重建后的数据行。</param>
/// <param name="Diagnostics">诊断信息。</param>
/// <param name="MissingCells">未被整表 OCR 识别到的数据格。</param>
/// <param name="Layout">本次 OCR 推断出的表格几何布局；行或列信息不足时为 <see langword="null"/>。</param>
internal sealed record GameDataTableParseResult(
    IReadOnlyList<GameDataTableRow> Rows,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<GameDataTableMissingCell> MissingCells,
    GameDataTableLayout? Layout);
