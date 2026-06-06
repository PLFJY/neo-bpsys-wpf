#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class GameImportCompatibilityTest
{
    [Fact]
    public async Task ImportGamePreservesSerializedMatchScoreAndRefreshesScoreService()
    {
        var sourceGame = CreateGameWithRichMatchScore();
        var filePath = WriteGameJson(sourceGame);
        var sharedDataService = CreateSharedDataService();
        var matchScoreService = new MatchScoreService(
            sharedDataService,
            NullLogger<MatchScoreService>.Instance);

        try
        {
            await sharedDataService.ImportGameAsync(filePath);

            var importedScore = sharedDataService.CurrentGame.MatchScore;
            Assert.Equal(7, importedScore.Games.Count);
            Assert.Equal(GameResult.Escape3, importedScore.GetHalf(GameProgress.Game1FirstHalf)!.Result);
            Assert.Equal(GameResult.Out4, importedScore.GetHalf(GameProgress.Game1SecondHalf)!.Result);
            Assert.Equal(GameResult.Tie,
                importedScore.Games.Single(game => game.Key == new ScoreGameKey(3, ScoreGameKind.Overtime))
                    .FirstHalf.Result);
            Assert.Equal(GameResult.Escape4,
                importedScore.Games.Single(game => game.Key == new ScoreGameKey(4, ScoreGameKind.Normal))
                    .FirstHalf.Result);
            Assert.Equal(GameResult.Escape4,
                importedScore.Games.Single(game => game.Key == new ScoreGameKey(5, ScoreGameKind.Normal))
                    .SecondHalf.Result);

            Assert.Equal("3", importedScore.CurrentSurTeamPreHalfMinorScoreText);
            Assert.Equal("1", importedScore.CurrentHunTeamPreHalfMinorScoreText);

            Assert.Same(importedScore, matchScoreService.Current);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportOldGameWithoutMatchScorePreservesLegacyMirrorAndMissingMapDefaults()
    {
        var sourceGame = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1SecondHalf);
        var json = CreateOldJson(sourceGame, removeMapKey: Map.ChinaTown.ToString());
        var filePath = WriteJson(json);
        var sharedDataService = CreateSharedDataService();
        _ = new MatchScoreService(sharedDataService, NullLogger<MatchScoreService>.Instance);

        try
        {
            await sharedDataService.ImportGameAsync(filePath);

            Assert.All(sharedDataService.CurrentGame.MatchScore.Games.SelectMany(game => new[] { game.FirstHalf, game.SecondHalf }),
                half => Assert.Null(half.Result));
            Assert.Equal(0, sharedDataService.CurrentGame.MatchScore.HomeTotalMinorScore);
            Assert.Equal(0, sharedDataService.CurrentGame.MatchScore.AwayTotalMinorScore);
            Assert.Equal(2, sharedDataService.CurrentGame.SurTeam.Score.Win);
            Assert.Equal(1, sharedDataService.CurrentGame.SurTeam.Score.Tie);
            Assert.Equal(7, sharedDataService.CurrentGame.SurTeam.Score.GameScores);
            Assert.Equal(1, sharedDataService.CurrentGame.HunTeam.Score.Win);
            Assert.Equal(0, sharedDataService.CurrentGame.HunTeam.Score.Tie);
            Assert.Equal(5, sharedDataService.CurrentGame.HunTeam.Score.GameScores);
            Assert.True(sharedDataService.CurrentGame.MapV2Dictionary.ContainsKey(Map.ChinaTown.ToString()));
            Assert.NotEmpty(sharedDataService.CurrentGame.CurrentSurBannedList);
            Assert.NotEmpty(sharedDataService.CurrentGame.CurrentHunBannedList);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportGameDoesNotOverwriteValidMatchScoreWithLegacyMirror()
    {
        var sourceGame = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1SecondHalf);
        var half = sourceGame.MatchScore.GetHalf(GameProgress.Game1FirstHalf)!;
        half.Result = GameResult.Escape4;
        half.SurTeamTypeWhenRecorded = TeamType.HomeTeam;
        half.HunTeamTypeWhenRecorded = TeamType.AwayTeam;
        sourceGame.MatchScore.Recalculate(null);

        var node = JsonNode.Parse(JsonSerializer.Serialize(sourceGame, CreateJsonOptions()))!.AsObject();
        SetLegacyScore(node[nameof(Game.SurTeam)]!, win: 9, tie: 9, gameScores: 99);
        SetLegacyScore(node[nameof(Game.HunTeam)]!, win: 8, tie: 8, gameScores: 88);
        var filePath = WriteJson(node.ToJsonString());
        var sharedDataService = CreateSharedDataService();
        _ = new MatchScoreService(sharedDataService, NullLogger<MatchScoreService>.Instance);

        try
        {
            await sharedDataService.ImportGameAsync(filePath);

            Assert.Equal(GameResult.Escape4,
                sharedDataService.CurrentGame.MatchScore.GetHalf(GameProgress.Game1FirstHalf)!.Result);
            Assert.Equal(5, sharedDataService.CurrentGame.MatchScore.HomeTotalMinorScore);
            Assert.Equal(0, sharedDataService.CurrentGame.MatchScore.AwayTotalMinorScore);
            Assert.Equal(0, sharedDataService.CurrentGame.SurTeam.Score.Win);
            Assert.Equal(0, sharedDataService.CurrentGame.SurTeam.Score.Tie);
            Assert.Equal(5, sharedDataService.CurrentGame.SurTeam.Score.GameScores);
            Assert.Equal(0, sharedDataService.CurrentGame.HunTeam.Score.GameScores);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static Game CreateGameWithRichMatchScore()
    {
        var game = new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Game1SecondHalf);
        SetHalf(game.MatchScore.GetHalf(GameProgress.Game1FirstHalf)!, GameResult.Escape3, TeamType.HomeTeam, TeamType.AwayTeam);
        SetHalf(game.MatchScore.GetHalf(GameProgress.Game1SecondHalf)!, GameResult.Out4, TeamType.AwayTeam, TeamType.HomeTeam);
        SetHalf(game.MatchScore.GetHalf(GameProgress.Game2FirstHalf)!, GameResult.Tie, TeamType.HomeTeam, TeamType.AwayTeam);
        SetHalf(game.MatchScore.GetHalf(GameProgress.Game2SecondHalf)!, GameResult.Tie, TeamType.AwayTeam, TeamType.HomeTeam);
        SetHalf(
            game.MatchScore.Games.Single(scoreGame => scoreGame.Key == new ScoreGameKey(3, ScoreGameKind.Overtime)).FirstHalf,
            GameResult.Tie,
            TeamType.HomeTeam,
            TeamType.AwayTeam);
        SetHalf(
            game.MatchScore.Games.Single(scoreGame => scoreGame.Key == new ScoreGameKey(4, ScoreGameKind.Normal)).FirstHalf,
            GameResult.Escape4,
            TeamType.HomeTeam,
            TeamType.AwayTeam);
        SetHalf(
            game.MatchScore.Games.Single(scoreGame => scoreGame.Key == new ScoreGameKey(4, ScoreGameKind.Normal)).SecondHalf,
            GameResult.Out4,
            TeamType.AwayTeam,
            TeamType.HomeTeam);
        SetHalf(
            game.MatchScore.Games.Single(scoreGame => scoreGame.Key == new ScoreGameKey(5, ScoreGameKind.Normal)).FirstHalf,
            GameResult.Out4,
            TeamType.HomeTeam,
            TeamType.AwayTeam);
        SetHalf(
            game.MatchScore.Games.Single(scoreGame => scoreGame.Key == new ScoreGameKey(5, ScoreGameKind.Normal)).SecondHalf,
            GameResult.Escape4,
            TeamType.AwayTeam,
            TeamType.HomeTeam);
        game.MatchScore.Recalculate(null);
        return game;
    }

    private static string CreateOldJson(Game game, string removeMapKey)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(game, CreateJsonOptions()))!.AsObject();
        node.Remove(nameof(Game.MatchScore));
        node[nameof(Game.CurrentSurBannedList)] = new JsonArray();
        node[nameof(Game.CurrentHunBannedList)] = new JsonArray();
        node[nameof(Game.MapV2Dictionary)]?.AsObject().Remove(removeMapKey);
        SetLegacyScore(node[nameof(Game.SurTeam)]!, win: 2, tie: 1, gameScores: 7);
        SetLegacyScore(node[nameof(Game.HunTeam)]!, win: 1, tie: 0, gameScores: 5);
        return node.ToJsonString();
    }

    private static void SetHalf(ScoreHalf half, GameResult result, TeamType surTeamType, TeamType hunTeamType)
    {
        half.Result = result;
        half.SurTeamTypeWhenRecorded = surTeamType;
        half.HunTeamTypeWhenRecorded = hunTeamType;
    }

    private static void SetLegacyScore(JsonNode teamNode, int win, int tie, int gameScores)
    {
        teamNode["Score"] = new JsonObject
        {
            ["Win"] = win,
            ["Tie"] = tie,
            ["GameScores"] = gameScores
        };
    }

    private static SharedDataService CreateSharedDataService()
    {
        var settingsHostService = new Mock<ISettingsHostService>();
        settingsHostService.SetupProperty(service => service.Settings, new Settings());
        return new SharedDataService(settingsHostService.Object, NullLogger<SharedDataService>.Instance);
    }

    private static string WriteGameJson(Game game) =>
        WriteJson(JsonSerializer.Serialize(game, CreateJsonOptions()));

    private static string WriteJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-bpsys-game-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static JsonSerializerOptions CreateJsonOptions() =>
        new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
}
