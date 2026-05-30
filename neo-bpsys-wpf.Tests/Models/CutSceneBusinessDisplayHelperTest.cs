using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Helpers;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class CutSceneBusinessDisplayHelperTest
{
    [Theory]
    [InlineData(GameProgress.Game1FirstHalf, false, "GAME 1 FIRST HALF")]
    [InlineData(GameProgress.Game1SecondHalf, false, "GAME 1 SECOND HALF")]
    [InlineData(GameProgress.Game4FirstHalf, true, "GAME 3 OVERTIME FIRST HALF")]
    [InlineData(GameProgress.Game4FirstHalf, false, "GAME 4 FIRST HALF")]
    [InlineData(GameProgress.Game5OvertimeSecondHalf, false, "GAME 5 OVERTIME SECOND HALF")]
    public void GameProgressDisplayHelperFormatsKnownProgress(
        GameProgress progress,
        bool isBo3Mode,
        string expected)
    {
        Assert.Equal(expected, GameProgressDisplayHelper.Format(progress, isBo3Mode));
    }

    [Fact]
    public void GameProgressDisplayHelperSupportsLineBreak()
    {
        Assert.Equal(
            "GAME 3 OVERTIME\nFIRST HALF",
            GameProgressDisplayHelper.Format(GameProgress.Game4FirstHalf, isBo3Mode: true, useLineBreak: true));
    }

    [Fact]
    public void MapNameDisplayHelperLocalizesKnownMapKey()
    {
        var text = MapNameDisplayHelper.Format(Map.ChinaTown);

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void MapNameDisplayHelperUsesEmptyTextWhenMapIsNull()
    {
        Assert.Equal("-", MapNameDisplayHelper.Format(null, "-"));
    }
}
