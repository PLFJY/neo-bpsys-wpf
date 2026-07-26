using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class ScoreSystemModelTest
{
    public static IEnumerable<object[]> MinorScoreCases =>
    [
        [GameResult.Escape4, 5, 0],
        [GameResult.Escape3, 3, 1],
        [GameResult.Tie, 2, 2],
        [GameResult.Out3, 1, 3],
        [GameResult.Out4, 0, 5]
    ];

    [Theory]
    [MemberData(nameof(MinorScoreCases))]
    public void ScoreHalfMapsGameResultToSurAndHunMinorScore(GameResult result, int surScore, int hunScore)
    {
        var half = new ScoreHalf(GameProgress.Game1FirstHalf, ScoreHalfKind.FirstHalf)
        {
            Result = result
        };

        Assert.Equal(surScore, half.SurMinorScore);
        Assert.Equal(hunScore, half.HunMinorScore);
    }

    [Fact]
    public void ScoreHalfMapsRecordedSurAndHunTeamTypesToHomeAndAway()
    {
        var half = new ScoreHalf(GameProgress.Game1FirstHalf, ScoreHalfKind.FirstHalf)
        {
            Result = GameResult.Escape3,
            SurTeamTypeWhenRecorded = TeamType.AwayTeam,
            HunTeamTypeWhenRecorded = TeamType.HomeTeam
        };

        Assert.Equal(1, half.HomeMinorScore);
        Assert.Equal(3, half.AwayMinorScore);
        Assert.Equal("1", half.HomeDisplayText);
        Assert.Equal("3", half.AwayDisplayText);
    }

    [Fact]
    public void ScoreHalfWithoutResultDoesNotCalculate()
    {
        var half = new ScoreHalf(GameProgress.Game1FirstHalf, ScoreHalfKind.FirstHalf)
        {
            SurTeamTypeWhenRecorded = TeamType.HomeTeam,
            HunTeamTypeWhenRecorded = TeamType.AwayTeam
        };

        Assert.False(half.HasResult);
        Assert.Null(half.SurMinorScore);
        Assert.Null(half.HunMinorScore);
        Assert.Null(half.HomeMinorScore);
        Assert.Null(half.AwayMinorScore);
        Assert.Equal("-", half.HomeDisplayText);
        Assert.Equal("-", half.AwayDisplayText);
    }

    [Fact]
    public void ScoreGameDerivesMinorScoreAndMajorResult()
    {
        var game = new ScoreGame(
            new ScoreGameKey(1, ScoreGameKind.Normal),
            new ScoreHalf(GameProgress.Game1FirstHalf, ScoreHalfKind.FirstHalf)
            {
                Result = GameResult.Escape3,
                SurTeamTypeWhenRecorded = TeamType.HomeTeam,
                HunTeamTypeWhenRecorded = TeamType.AwayTeam
            },
            new ScoreHalf(GameProgress.Game1SecondHalf, ScoreHalfKind.SecondHalf)
            {
                Result = GameResult.Out4,
                SurTeamTypeWhenRecorded = TeamType.AwayTeam,
                HunTeamTypeWhenRecorded = TeamType.HomeTeam
            });

        Assert.True(game.IsComplete);
        Assert.Equal(8, game.HomeMinorScore);
        Assert.Equal(1, game.AwayMinorScore);
        Assert.Equal(ScoreGameMajorResult.HomeWin, game.MajorResult);
    }

    [Fact]
    public void MatchScoreStateCreateDefaultCreatesExpectedScoreGames()
    {
        var state = MatchScoreState.CreateDefault();

        Assert.Equal(
            [
                new ScoreGameKey(1, ScoreGameKind.Normal),
                new ScoreGameKey(2, ScoreGameKind.Normal),
                new ScoreGameKey(3, ScoreGameKind.Normal),
                new ScoreGameKey(3, ScoreGameKind.Overtime),
                new ScoreGameKey(4, ScoreGameKind.Normal),
                new ScoreGameKey(5, ScoreGameKind.Normal),
                new ScoreGameKey(5, ScoreGameKind.Overtime)
            ],
            state.Games.Select(game => game.Key).ToArray());
    }

    [Fact]
    public void MatchScoreStateMapsGameProgressExplicitly()
    {
        var state = MatchScoreState.CreateDefault();

        Assert.Equal(new ScoreGameKey(1, ScoreGameKind.Normal), state.GetGame(GameProgress.Game1FirstHalf)?.Key);
        Assert.Equal(new ScoreGameKey(2, ScoreGameKind.Normal), state.GetGame(GameProgress.Game2SecondHalf)?.Key);
        Assert.Equal(new ScoreGameKey(3, ScoreGameKind.Normal), state.GetGame(GameProgress.Game3FirstHalf)?.Key);
        Assert.Equal(new ScoreGameKey(3, ScoreGameKind.Overtime),
            state.GetGame(GameProgress.Game3OvertimeFirstHalf, isBo3Mode: true)?.Key);
        Assert.Equal(new ScoreGameKey(4, ScoreGameKind.Normal), state.GetGame(GameProgress.Game4FirstHalf)?.Key);
        Assert.Equal(new ScoreGameKey(5, ScoreGameKind.Normal), state.GetGame(GameProgress.Game5SecondHalf)?.Key);
        Assert.Equal(new ScoreGameKey(5, ScoreGameKind.Overtime),
            state.GetGame(GameProgress.Game5OvertimeSecondHalf)?.Key);
        Assert.Null(state.GetGame(GameProgress.Free));
        Assert.Null(state.GetHalf(GameProgress.Free));
    }

    [Fact]
    public void Bo3RecalculateIncludesGame3Overtime()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(1, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(2, ScoreGameKind.Normal), GameResult.Out4);
        FillGame(state, new ScoreGameKey(3, ScoreGameKind.Normal), GameResult.Tie);
        FillGame(state, new ScoreGameKey(3, ScoreGameKind.Overtime), GameResult.Escape3);
        FillGame(state, new ScoreGameKey(4, ScoreGameKind.Normal), GameResult.Out4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Normal), GameResult.Out4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Overtime), GameResult.Out4);

        state.Recalculate(isBo3Mode: true);

        Assert.Equal(20, state.HomeTotalMinorScore);
        Assert.Equal(16, state.AwayTotalMinorScore);
        Assert.Equal(2, state.HomeMajorWin);
        Assert.Equal(1, state.AwayMajorWin);
        Assert.Equal(1, state.HomeMajorTie);
        Assert.Equal(1, state.AwayMajorTie);
    }

    [Fact]
    public void Bo3RecalculateExcludesBo5OnlyGames()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(4, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Overtime), GameResult.Escape4);

        state.Recalculate(isBo3Mode: true);

        Assert.Equal(0, state.HomeTotalMinorScore);
        Assert.Equal(0, state.AwayTotalMinorScore);
        Assert.Equal(0, state.HomeMajorWin);
        Assert.Equal(0, state.AwayMajorWin);
        Assert.Equal(0, state.HomeMajorTie);
        Assert.Equal(0, state.AwayMajorTie);
    }

    [Fact]
    public void Bo5RecalculateIncludesGame5Overtime()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(1, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(2, ScoreGameKind.Normal), GameResult.Tie);
        FillGame(state, new ScoreGameKey(3, ScoreGameKind.Normal), GameResult.Out4);
        FillGame(state, new ScoreGameKey(4, ScoreGameKind.Normal), GameResult.Escape3);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Overtime), GameResult.Out3);

        state.Recalculate(isBo3Mode: false);

        Assert.Equal(32, state.HomeTotalMinorScore);
        Assert.Equal(22, state.AwayTotalMinorScore);
        Assert.Equal(3, state.HomeMajorWin);
        Assert.Equal(2, state.AwayMajorWin);
        Assert.Equal(1, state.HomeMajorTie);
        Assert.Equal(1, state.AwayMajorTie);
    }

    [Fact]
    public void Bo5RecalculateExcludesGame3Overtime()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(3, ScoreGameKind.Overtime), GameResult.Escape4);

        state.Recalculate(isBo3Mode: false);

        Assert.Equal(0, state.HomeTotalMinorScore);
        Assert.Equal(0, state.AwayTotalMinorScore);
        Assert.Equal(0, state.HomeMajorWin);
        Assert.Equal(0, state.AwayMajorWin);
    }

    [Fact]
    public void SwitchingBo5ToBo3RecalculatesTotals()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(3, ScoreGameKind.Overtime), GameResult.Escape3);
        FillGame(state, new ScoreGameKey(4, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Normal), GameResult.Escape4);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Overtime), GameResult.Escape4);
        state.Recalculate(isBo3Mode: false);

        state.Recalculate(isBo3Mode: true);

        Assert.Equal(6, state.HomeTotalMinorScore);
        Assert.Equal(2, state.AwayTotalMinorScore);
        Assert.Equal(1, state.HomeMajorWin);
        Assert.Equal(0, state.AwayMajorWin);
    }

    [Fact]
    public void SwitchingBo3ToBo5RecalculatesTotals()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(3, ScoreGameKind.Overtime), GameResult.Escape3);
        FillGame(state, new ScoreGameKey(5, ScoreGameKind.Overtime), GameResult.Out3);
        state.Recalculate(isBo3Mode: true);

        state.Recalculate(isBo3Mode: false);

        Assert.Equal(2, state.HomeTotalMinorScore);
        Assert.Equal(6, state.AwayTotalMinorScore);
        Assert.Equal(0, state.HomeMajorWin);
        Assert.Equal(1, state.AwayMajorWin);
    }

    [Fact]
    public void FreeProgressDoesNotLeaveStaleTotals()
    {
        var state = MatchScoreState.CreateDefault();
        FillGame(state, new ScoreGameKey(4, ScoreGameKind.Normal), GameResult.Escape4);
        state.Recalculate(isBo3Mode: false);
        Assert.Equal(10, state.HomeTotalMinorScore);

        state.Recalculate(isBo3Mode: true);

        Assert.Equal(0, state.HomeTotalMinorScore);
        Assert.Equal(0, state.AwayTotalMinorScore);
    }

    [Fact]
    public void GameConstructedWithoutMatchScoreGetsDefaultMatchScore()
    {
        var game = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Free);

        Assert.NotNull(game.MatchScore);
        Assert.Equal(7, game.MatchScore.Games.Count);
    }

    [Fact]
    public void GameDeserializesFromJsonWithoutMatchScore()
    {
        var options = CreateJsonOptions();
        var game = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1FirstHalf);
        var node = JsonNode.Parse(JsonSerializer.Serialize(game, options))!.AsObject();
        Assert.True(node.Remove(nameof(Game.MatchScore)));

        var deserialized = JsonSerializer.Deserialize<Game>(node.ToJsonString(), options);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.MatchScore);
        Assert.Equal(7, deserialized.MatchScore.Games.Count);
    }

    [Fact]
    public void GameDeserializesFromJsonWithMatchScoreRoundData()
    {
        var options = CreateJsonOptions();
        var game = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1FirstHalf);
        var half = game.MatchScore.GetHalf(GameProgress.Game1FirstHalf)!;
        half.Result = GameResult.Escape3;
        half.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        half.HunTeamTypeWhenRecorded = TeamType.AwayTeam;

        var json = JsonSerializer.Serialize(game, options);
        var deserialized = JsonSerializer.Deserialize<Game>(json, options);
        var deserializedHalf = deserialized!.MatchScore.GetHalf(GameProgress.Game1FirstHalf)!;

        Assert.Equal(GameResult.Escape3, deserializedHalf.Result);
        Assert.Equal(TeamType.HomeTeam, deserializedHalf.SurTeamTypeWhenRecorded);
        Assert.Equal(TeamType.AwayTeam, deserializedHalf.HunTeamTypeWhenRecorded);
        Assert.Equal(3, deserializedHalf.HomeMinorScore);
        Assert.Equal(1, deserializedHalf.AwayMinorScore);
        Assert.Equal(3, deserialized.MatchScore.HomeTotalMinorScore);
        Assert.Equal(1, deserialized.MatchScore.AwayTotalMinorScore);
    }

    [Fact]
    public void MatchScoreStateCloneCreatesIndependentMutableCopy()
    {
        var state = MatchScoreState.CreateDefault();
        var originalHalf = state.GetHalf(GameProgress.Game1FirstHalf)!;
        originalHalf.Result = GameResult.Escape4;
        originalHalf.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        originalHalf.HunTeamTypeWhenRecorded = TeamType.AwayTeam;

        var clone = state.Clone();
        clone.GetHalf(GameProgress.Game1FirstHalf)!.Result = GameResult.Out4;

        Assert.Equal(GameResult.Escape4, state.GetHalf(GameProgress.Game1FirstHalf)!.Result);
        Assert.Equal(GameResult.Out4, clone.GetHalf(GameProgress.Game1FirstHalf)!.Result);
    }

    private static JsonSerializerOptions CreateJsonOptions() =>
        new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

    private static void FillGame(MatchScoreState state, ScoreGameKey key, GameResult result)
    {
        var game = state.Games.Single(scoreGame => scoreGame.Key == key);
        SetHalf(game.FirstHalf, result);
        SetHalf(game.SecondHalf, result);
    }

    private static void SetHalf(ScoreHalf half, GameResult result)
    {
        half.Result = result;
        half.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        half.HunTeamTypeWhenRecorded = TeamType.AwayTeam;
    }
}
