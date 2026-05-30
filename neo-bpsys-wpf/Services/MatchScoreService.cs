using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.ComponentModel;
using System.Globalization;

namespace neo_bpsys_wpf.Services;

public class MatchScoreService : IMatchScoreService
{
    private readonly ISharedDataService _sharedDataService;
    private readonly ILogger<MatchScoreService> _logger;
    private Game? _subscribedGame;

    public MatchScoreService(ISharedDataService sharedDataService, ILogger<MatchScoreService> logger)
    {
        _sharedDataService = sharedDataService;
        _logger = logger;
        SubscribeGame(_sharedDataService.CurrentGame);
        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
    }

    public MatchScoreState Current => _sharedDataService.CurrentGame.MatchScore;

    public ScoreHalf? CurrentHalf => GetHalf(_sharedDataService.CurrentGame.GameProgress);

    public ScoreGame? CurrentGameScore => GetGame(_sharedDataService.CurrentGame.GameProgress);

    public ScoreHalf? GetHalf(GameProgress progress) => Current.GetHalf(progress, _sharedDataService.IsBo3Mode);

    public ScoreGame? GetGame(GameProgress progress) => Current.GetGame(progress, _sharedDataService.IsBo3Mode);

    public void SetCurrentHalfResult(GameResult? result)
    {
        var half = CurrentHalf;
        if (half == null)
        {
            _logger.LogDebug(
                "Skipped score result update because progress {GameProgress} is not supported.",
                _sharedDataService.CurrentGame.GameProgress);
            return;
        }

        half.Result = result;
        if (result == null)
        {
            half.SurTeamTypeWhenRecorded = null;
            half.HunTeamTypeWhenRecorded = null;
        }
        else
        {
            half.SurTeamTypeWhenRecorded = _sharedDataService.CurrentGame.SurTeam.TeamType;
            half.HunTeamTypeWhenRecorded = _sharedDataService.CurrentGame.HunTeam.TeamType;
        }

        Recalculate();
        RefreshCurrentProgress();
        SyncLegacyTeamScoreMirror();
    }

    public void ClearCurrentHalfResult() => SetCurrentHalfResult(null);

    public void Recalculate() => Current.Recalculate();

    public void RefreshCurrentProgress()
    {
        Current.RefreshCurrentDisplay(
            _sharedDataService.CurrentGame.GameProgress,
            _sharedDataService.CurrentGame.SurTeam.TeamType,
            _sharedDataService.CurrentGame.HunTeam.TeamType,
            _sharedDataService.IsBo3Mode);
    }

    public void SyncLegacyTeamScoreMirror()
    {
        var currentGame = _sharedDataService.CurrentGame;

        // Transitional compatibility only: Team.Score is no longer authoritative for new score code.
        currentGame.SurTeam.Score.GameScores = ParseMinorScore(Current.CurrentSurTeamPreHalfMinorScoreText);
        currentGame.HunTeam.Score.GameScores = ParseMinorScore(Current.CurrentHunTeamPreHalfMinorScoreText);

        SyncMajorMirror(currentGame.SurTeam);
        SyncMajorMirror(currentGame.HunTeam);
    }

    private void SyncMajorMirror(Team team)
    {
        if (team.TeamType == TeamType.HomeTeam)
        {
            team.Score.Win = Current.HomeMajorWin;
            team.Score.Tie = Current.HomeMajorTie;
            return;
        }

        team.Score.Win = Current.AwayMajorWin;
        team.Score.Tie = Current.AwayMajorTie;
    }

    private static int ParseMinorScore(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ? score : 0;

    private void OnCurrentGameChanged(object? sender, EventArgs args)
    {
        SubscribeGame(_sharedDataService.CurrentGame);
        Recalculate();
        RefreshCurrentProgress();
        SyncLegacyTeamScoreMirror();
    }

    private void SubscribeGame(Game game)
    {
        if (_subscribedGame != null)
        {
            _subscribedGame.PropertyChanged -= OnCurrentGamePropertyChanged;
            _subscribedGame.TeamSwapped -= OnTeamSwapped;
        }

        _subscribedGame = game;
        _subscribedGame.PropertyChanged += OnCurrentGamePropertyChanged;
        _subscribedGame.TeamSwapped += OnTeamSwapped;
    }

    private void OnCurrentGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Game.GameProgress))
            return;

        RefreshCurrentProgress();
        SyncLegacyTeamScoreMirror();
    }

    private void OnTeamSwapped(object? sender, EventArgs args)
    {
        RefreshCurrentProgress();
        SyncLegacyTeamScoreMirror();
    }
}
