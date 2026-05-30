using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.ComponentModel;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class ScorePageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public ScorePageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IMatchScoreService _matchScoreService;
    private Game? _subscribedGame;

    public ScorePageViewModel(
        ISharedDataService sharedDataService,
        IMatchScoreService matchScoreService)
    {
        _sharedDataService = sharedDataService;
        _matchScoreService = matchScoreService;
        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshScorePageState();
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;
    public Team HomeTeam => _sharedDataService.HomeTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    #region 比分控制

    [RelayCommand]
    private void Escape4()
    {
        SetCurrentHalfResult(GameResult.Escape4);
    }

    [RelayCommand]
    private void Escape3()
    {
        SetCurrentHalfResult(GameResult.Escape3);
    }

    [RelayCommand]
    private void Tie()
    {
        SetCurrentHalfResult(GameResult.Tie);
    }

    [RelayCommand]
    private void Out3()
    {
        SetCurrentHalfResult(GameResult.Out3);
    }

    [RelayCommand]
    private void Out4()
    {
        SetCurrentHalfResult(GameResult.Out4);
    }

    [RelayCommand]
    private void ClearCurrentHalfScore()
    {
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free)
            return;

        _matchScoreService.ClearCurrentHalfResult();
        RefreshScorePageState();
    }

    [RelayCommand]
    private void Reset()
    {
        foreach (var scoreGame in _matchScoreService.Current.Games)
        {
            ClearHalf(scoreGame.FirstHalf);
            ClearHalf(scoreGame.SecondHalf);
        }

        _matchScoreService.Recalculate();
        _matchScoreService.RefreshCurrentProgress();
        _matchScoreService.SyncLegacyTeamScoreMirror();
        RefreshScorePageState();
    }

    #endregion

    #region 分数统计

    private void SetCurrentHalfResult(GameResult result)
    {
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free)
            return;

        _matchScoreService.SetCurrentHalfResult(result);
        RefreshScorePageState();
    }

    private void RefreshScorePageState()
    {
        _matchScoreService.Recalculate();
        _matchScoreService.RefreshCurrentProgress();
        OnPropertyChanged(nameof(CurrentGame));
        OnPropertyChanged(nameof(HomeTeam));
        OnPropertyChanged(nameof(AwayTeam));
    }

    private void OnCurrentGameChanged(object? sender, EventArgs args)
    {
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshScorePageState();
    }

    private void SubscribeGame(Game game)
    {
        if (_subscribedGame != null)
        {
            _subscribedGame.PropertyChanged -= OnCurrentGamePropertyChanged;
        }

        _subscribedGame = game;
        _subscribedGame.PropertyChanged += OnCurrentGamePropertyChanged;
    }

    private void OnCurrentGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(Game.GameProgress))
            return;

        RefreshScorePageState();
    }

    private static void ClearHalf(ScoreHalf half)
    {
        half.Result = null;
        half.SurTeamTypeWhenRecorded = null;
        half.HunTeamTypeWhenRecorded = null;
    }

    #endregion
}
