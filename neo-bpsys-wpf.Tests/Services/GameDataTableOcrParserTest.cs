extern alias smartbp;

using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using GameDataTableOcrParser = smartbp::neo_bpsys_wpf.Services.GameDataTableOcrParser;
using OcrTextLine = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrTextLine;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class GameDataTableOcrParserTest
{
    [Fact]
    public void Parse_RebuildsRowsFromShuffledCoordinatesAndIgnoresTalentNumbers()
    {
        var lines = new List<OcrTextLine>();
        for (var row = 0; row < 5; row++)
        {
            var nameY = 40 + row * 100;
            lines.Add(Line($"玩家{row}(角色{row})", 80, nameY));
            lines.Add(Line("130", 100, nameY + 42));
            for (var column = 0; column < 5; column++)
                lines.Add(Line($"{row}{column + 1}", 250 + column * 110, nameY + 35 + (column % 2 == 0 ? 2 : -2)));
        }

        var result = GameDataTableOcrParser.Parse(lines.OrderByDescending(line => line.CenterX).ThenByDescending(line => line.CenterY).ToArray());

        Assert.Equal(5, result.Rows.Count);
        for (var row = 0; row < 5; row++)
        {
            Assert.Equal($"玩家{row}", result.Rows[row].PlayerName);
            Assert.Equal($"角色{row}", result.Rows[row].CharacterName);
            Assert.Equal(new[] { $"{row}1", $"{row}2", $"{row}3", $"{row}4", $"{row}5" }, result.Rows[row].Values);
        }
        Assert.Contains(result.Diagnostics, message => message.Contains("ignored name-column text", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_IgnoresInvalidNamesAndDoesNotDuplicateDataValues()
    {
        var result = GameDataTableOcrParser.Parse(
        [
            Line("无角色括号", 80, 40),
            Line("玩家(角色)", 80, 140),
            Line("7", 250, 174),
            Line("8", 250, 175),
            Line("2", 360, 174),
            Line("3", 470, 174),
            Line("4", 580, 174),
            Line("5", 690, 174)
        ]);

        var row = Assert.Single(result.Rows);
        Assert.Equal("玩家", row.PlayerName);
        Assert.Equal("角色", row.CharacterName);
        Assert.Equal(new[] { "7", "2", "3", "4", "5" }, row.Values);
        Assert.Contains(result.Diagnostics, message => message.Contains("ignored duplicate data", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_LeavesMissingColumnsEmptyAndReturnsOnlyRecognizedRows()
    {
        var result = GameDataTableOcrParser.Parse(
        [
            Line("无效玩家", 80, 40),
            Line("玩家甲(记者)", 80, 140),
            Line("91%", 250, 174),
            Line("3", 470, 174),
            Line("46", 690, 174)
        ]);

        var row = Assert.Single(result.Rows);
        Assert.Equal("玩家甲", row.PlayerName);
        Assert.Equal("记者", row.CharacterName);
        Assert.Equal(new[] { "91", "", "3", "", "46" }, row.Values);
        Assert.False(row.HasAllDataColumns);
    }

    [Fact]
    public void ParserReturnsStableFiveColumnLayout()
    {
        var lines = new List<OcrTextLine>();
        for (var row = 0; row < 5; row++)
        {
            var nameY = 140 + row * 100;
            lines.Add(Line($"玩家{row}(角色{row})", 80, nameY));
            for (var col = 0; col < 5; col++)
                lines.Add(Line($"{row}{col + 1}", 250 + col * 110, nameY + 35));
        }

        var result = GameDataTableOcrParser.Parse(lines);

        Assert.NotNull(result.Layout);
        Assert.Equal(5, result.Layout!.ColumnCenters.Count);
        Assert.Equal(5, result.Layout.RowCenters.Count);
        Assert.Equal(5, result.Layout.ColumnCenters.Distinct().Count());
        for (var i = 0; i < 5; i++)
            Assert.Equal(250 + i * 110, result.Layout.ColumnCenters[i], 1);
    }

    [Fact]
    public void NumericGridBoundsContainAllExpectedCellCenters()
    {
        var lines = new List<OcrTextLine>();
        for (var row = 0; row < 5; row++)
        {
            var nameY = 140 + row * 100;
            lines.Add(Line($"玩家{row}(角色{row})", 80, nameY));
            for (var col = 0; col < 5; col++)
                lines.Add(Line($"{row}{col + 1}", 250 + col * 110, nameY + 35));
        }

        var result = GameDataTableOcrParser.Parse(lines);
        Assert.NotNull(result.Layout);

        var bounds = result.Layout!.NumericGridBounds;
        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                var cx = result.Layout.ColumnCenters[col];
                var cy = result.Layout.RowCenters[row];
                Assert.True(bounds.Contains(new OpenCvSharp.Point((int)cx, (int)cy)),
                    $"cell center ({cx},{cy}) for row={row} col={col} not in bounds {bounds}");
            }
        }
    }

    [Fact]
    public void LayoutIsUnavailableWhenNoRowsCanBeInferred()
    {
        var result = GameDataTableOcrParser.Parse(
        [
            Line("无效文本", 80, 40),
            Line("7", 250, 175)
        ]);

        Assert.Null(result.Layout);
    }

    private static OcrTextLine Line(string text, double x, double y) =>
        new(text, 1, new Rect((int)x - 20, (int)y - 10, 40, 20), x, y, "test");
}
