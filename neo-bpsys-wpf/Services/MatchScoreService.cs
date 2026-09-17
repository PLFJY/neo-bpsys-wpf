using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.ComponentModel;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// <see cref="IMatchScoreService"/> 的默认实现，桥接当前共享赛事状态和 Score System v2 模型。
/// </summary>
public class MatchScoreService : IMatchScoreService
{
    private readonly ISharedDataService _sharedDataService;
    private readonly ILogger<MatchScoreService> _logger;
    private Game? _subscribedGame;

    /// <summary>
    /// 初始化比分服务并订阅当前赛事、赛制和阵营切换事件。
    /// </summary>
    /// <param name="sharedDataService">共享赛事状态服务。</param>
    /// <param name="logger">日志记录器。</param>
    public MatchScoreService(ISharedDataService sharedDataService, ILogger<MatchScoreService> logger)
    {
        _sharedDataService = sharedDataService;
        _logger = logger;
        SubscribeGame(_sharedDataService.CurrentGame);
        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
        _sharedDataService.IsBo3ModeChanged += OnIsBo3ModeChanged;
    }

    /// <inheritdoc />
    public MatchScoreState Current => _sharedDataService.CurrentGame.MatchScore;

    /// <inheritdoc />
    public FreeMatchScoreState FreeScore => Current.FreeScore;

    /// <inheritdoc />
    public bool IsFreeMode => _sharedDataService.CurrentGame.GameProgress == GameProgress.Free;

    /// <inheritdoc />
    public ScoreHalf? CurrentHalf => GetHalf(_sharedDataService.CurrentGame.GameProgress);

    /// <inheritdoc />
    public ScoreGame? CurrentGameScore => GetGame(_sharedDataService.CurrentGame.GameProgress);

    /// <inheritdoc />
    public ScoreHalf? GetHalf(GameProgress progress) => Current.GetHalf(progress, _sharedDataService.IsBo3Mode);

    /// <inheritdoc />
    public ScoreGame? GetGame(GameProgress progress) => Current.GetGame(progress, _sharedDataService.IsBo3Mode);

    /// <inheritdoc />
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
    }

    /// <inheritdoc />
    public void ClearCurrentHalfResult() => SetCurrentHalfResult(null);

    /// <inheritdoc />
    public void ApplyFreeResultPreset(GameResult result)
    {
        if (!IsFreeMode)
            return;

        var (surScore, hunScore) = result switch
        {
            GameResult.Escape4 => (5, 0),
            GameResult.Escape3 => (3, 1),
            GameResult.Tie => (2, 2),
            GameResult.Out3 => (1, 3),
            GameResult.Out4 => (0, 5),
            _ => throw new InvalidEnumArgumentException(nameof(result), (int)result, typeof(GameResult))
        };

        AddFreeScore(_sharedDataService.CurrentGame.SurTeam.TeamType, surScore);
        AddFreeScore(_sharedDataService.CurrentGame.HunTeam.TeamType, hunScore);
        RefreshCurrentProgress();
    }

    /// <inheritdoc />
    public void SettleFreeMajorScore()
    {
        if (!IsFreeMode)
            return;

        if (FreeScore.Home.CurrentMinorScore > FreeScore.Away.CurrentMinorScore)
            FreeScore.Home.MajorWin++;
        else if (FreeScore.Away.CurrentMinorScore > FreeScore.Home.CurrentMinorScore)
            FreeScore.Away.MajorWin++;
        else
        {
            FreeScore.Home.MajorTie++;
            FreeScore.Away.MajorTie++;
        }

        ClearFreeCurrentMinorScore();
    }

    /// <inheritdoc />
    public void ClearFreeCurrentMinorScore()
    {
        if (!IsFreeMode)
            return;

        FreeScore.Home.CurrentMinorScore = 0;
        FreeScore.Away.CurrentMinorScore = 0;
        RefreshCurrentProgress();
    }

    /// <inheritdoc />
    public void ResetFreeScores()
    {
        if (!IsFreeMode)
            return;

        FreeScore.Reset();
        RefreshCurrentProgress();
    }

    /// <inheritdoc />
    public void Recalculate() => Current.Recalculate(_sharedDataService.IsBo3Mode);

    /// <inheritdoc />
    public void RefreshCurrentProgress()
    {
        Current.RefreshCurrentDisplay(
            _sharedDataService.CurrentGame.GameProgress,
            _sharedDataService.CurrentGame.SurTeam.TeamType,
            _sharedDataService.CurrentGame.HunTeam.TeamType,
            _sharedDataService.IsBo3Mode);
    }

    private void OnCurrentGameChanged(object? sender, EventArgs args)
    {
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshCurrentProgress();
        Recalculate();
        RefreshCurrentProgress();
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
        Recalculate();
        RefreshCurrentProgress();
    }

    private void OnTeamSwapped(object? sender, EventArgs args)
    {
        Recalculate();
        RefreshCurrentProgress();
    }

    private void OnIsBo3ModeChanged(object? sender, EventArgs args)
    {
        Recalculate();
        RefreshCurrentProgress();
    }

    private void AddFreeScore(TeamType teamType, int score)
    {
        var target = teamType == TeamType.HomeTeam ? FreeScore.Home : FreeScore.Away;
        target.CurrentMinorScore += score;
        target.TotalMinorScore += score;
    }
}
