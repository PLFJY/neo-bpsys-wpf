using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.ComponentModel;
using neo_bpsys_wpf.Core.Models;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class ScorePageViewModel : ViewModelBase, IRecipient<PropertyChangedMessage<bool>>
{
#pragma warning disable CS8618
    public ScorePageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IFrontedWindowService _frontedWindowService;
    private readonly IMatchScoreService _matchScoreService;
    private Game? _subscribedGame;
    private bool _isSyncingSelectedProgress;

    public ScorePageViewModel(
        ISharedDataService sharedDataService,
        IFrontedWindowService frontedWindowService,
        IMatchScoreService matchScoreService)
    {
        _sharedDataService = sharedDataService;
        _frontedWindowService = frontedWindowService;
        _matchScoreService = matchScoreService;
        _isBo3Mode = _sharedDataService.IsBo3Mode;
        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
        SubscribeGame(_sharedDataService.CurrentGame);
        RefreshScorePageState();
    }

    private void SyncHomeTeamCampFromGlobal(object? sender, EventArgs args)
    {
        if (SelectedGameResult == null)
        {
            HomeTeamCamp = CurrentHomeTeamCamp;
        }
    }

    [ObservableProperty] private bool _isDebugContentVisible =
#if DEBUG
            true
#else
        false
#endif
        ;

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
        UpdateScoreCompatibilityViews(_sharedDataService.CurrentGame.GameProgress);
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
        _frontedWindowService.ResetGlobalScore();
        RefreshScorePageState();
    }

    [RelayCommand]
    private void ResetGameScore()
    {
        ClearCurrentHalfScore();
    }

    [RelayCommand]
    private void CalculateMajorPoint()
    {
        _matchScoreService.Recalculate();
        _matchScoreService.SyncLegacyTeamScoreMirror();
        RefreshScorePageState();
    }

    [RelayCommand]
    private static void ManualControl()
    {
        IAppHost.Host?.Services.GetRequiredService<ScoreManualWindow>().ShowDialog();
    }

    #endregion

    #region 分数统计

    private bool _isBo3Mode;

    private bool IsBo3Mode
    {
        get => _isBo3Mode;
        set => SetPropertyWithAction(ref _isBo3Mode, value, _ =>
        {
            GameList = !value ? GameListBo5 : GameListBo3;
            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(HomeTeamCamp));
            OnPropertyChanged(nameof(SelectedGameResult));
            RefreshScorePageState();
        });
    }

    private GameProgress _selectedGameProgress = GameProgress.Game1FirstHalf;

    public GameProgress SelectedGameProgress
    {
        get => _selectedGameProgress;
        set
        {
            if (!SetProperty(ref _selectedGameProgress, value))
                return;

            if (!_isSyncingSelectedProgress && _sharedDataService.CurrentGame.GameProgress != value)
            {
                _sharedDataService.CurrentGame.GameProgress = value;
            }

            RefreshSelectedGameInfo();
            OnPropertyChanged(nameof(SelectedIndex));
        }
    }

    public int SelectedIndex => GameList.IndexOf(SelectedGameProgress);

    [RelayCommand]
    private void GlobalScoreUpdateToFront()
    {
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free)
            return;

        if (IsGameFinished && SelectedGameResult != null)
        {
            _matchScoreService.SetCurrentHalfResult(SelectedGameResult);
        }
        else
        {
            _matchScoreService.ClearCurrentHalfResult();
        }

        UpdateScoreCompatibilityViews(_sharedDataService.CurrentGame.GameProgress);
    }

    [ObservableProperty] private bool _isGameFinished;

    [ObservableProperty] private Camp? _homeTeamCamp = Camp.Sur;

    [ObservableProperty] private GameResult? _selectedGameResult;

    private void UpdateGlobalScore(GameProgress progress)
    {
        var half = _matchScoreService.GetHalf(progress);
        if (half is not { HasResult: true } ||
            half.HomeMinorScore == null ||
            half.AwayMinorScore == null)
        {
            _frontedWindowService.SetGlobalScoreToBar(TeamType.HomeTeam, progress);
            _frontedWindowService.SetGlobalScoreToBar(TeamType.AwayTeam, progress);
            return;
        }

        var homeCamp = GetRecordedCamp(half, TeamType.HomeTeam);
        var awayCamp = GetRecordedCamp(half, TeamType.AwayTeam);
        if (homeCamp == null || awayCamp == null)
            return;

        _frontedWindowService.SetGlobalScore(TeamType.HomeTeam, progress, homeCamp.Value, half.HomeMinorScore.Value);
        _frontedWindowService.SetGlobalScore(TeamType.AwayTeam, progress, awayCamp.Value, half.AwayMinorScore.Value);
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.PropertyName == nameof(ISharedDataService.IsBo3Mode))
        {
            IsBo3Mode = message.NewValue;
        }
    }

    private void UpdateTotalGameScore()
    {
        _matchScoreService.Recalculate();

        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<int>(this,
            nameof(ScoreWindowViewModel.TotalMainGameScore), 0, _matchScoreService.Current.HomeTotalMinorScore));
        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<int>(this,
            nameof(ScoreWindowViewModel.TotalAwayGameScore), 0, _matchScoreService.Current.AwayTotalMinorScore));
    }

    private void SetCurrentHalfResult(GameResult result)
    {
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free)
            return;

        _matchScoreService.SetCurrentHalfResult(result);
        UpdateScoreCompatibilityViews(_sharedDataService.CurrentGame.GameProgress);
    }

    private void UpdateScoreCompatibilityViews(GameProgress progress)
    {
        RefreshScorePageState();
        UpdateGlobalScore(progress);
        UpdateTotalGameScore();
        OnPropertyChanged(string.Empty);
    }

    private void RefreshScorePageState()
    {
        SyncGameGlobalInfoRecordFromMatchScore();
        RefreshSelectedProgressFromCurrentGame();
        RefreshSelectedGameInfo();
        UpdateTotalGameScore();
    }

    private void RefreshSelectedProgressFromCurrentGame()
    {
        var progress = _sharedDataService.CurrentGame.GameProgress;
        if (progress <= GameProgress.Free)
            return;

        _isSyncingSelectedProgress = true;
        try
        {
            SelectedGameProgress = progress;
        }
        finally
        {
            _isSyncingSelectedProgress = false;
        }
    }

    private void RefreshSelectedGameInfo()
    {
        var half = _matchScoreService.GetHalf(SelectedGameProgress);
        IsGameFinished = half?.Result != null;
        SelectedGameResult = half?.Result;
        HomeTeamCamp = GetHomeTeamCamp(half) ?? CurrentHomeTeamCamp;
    }

    private void SyncGameGlobalInfoRecordFromMatchScore()
    {
        foreach (var (progress, gameInfo) in GameGlobalInfoRecord)
        {
            var half = _matchScoreService.GetHalf(progress);
            gameInfo.IsGameFinished = half?.Result != null;
            gameInfo.HomeTeamCamp = GetHomeTeamCamp(half);
            gameInfo.GameResult = half?.Result;
        }
    }

    private Camp CurrentHomeTeamCamp => _sharedDataService.HomeTeam.Camp == Camp.Sur ? Camp.Sur : Camp.Hun;

    private static Camp? GetHomeTeamCamp(ScoreHalf? half)
    {
        if (half == null)
            return null;

        return GetRecordedCamp(half, TeamType.HomeTeam);
    }

    private static Camp? GetRecordedCamp(ScoreHalf half, TeamType teamType)
    {
        if (half.SurTeamTypeWhenRecorded == teamType)
            return Camp.Sur;

        if (half.HunTeamTypeWhenRecorded == teamType)
            return Camp.Hun;

        return null;
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
            _subscribedGame.TeamSwapped -= SyncHomeTeamCampFromGlobal;
            _subscribedGame.PropertyChanged -= OnCurrentGamePropertyChanged;
        }

        _subscribedGame = game;
        _subscribedGame.TeamSwapped += SyncHomeTeamCampFromGlobal;
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

    public OrderedDictionary<GameProgress, GameGlobalInfo> GameGlobalInfoRecord { get; } = new()
    {
        { GameProgress.Game1FirstHalf, new GameGlobalInfo() },
        { GameProgress.Game1SecondHalf, new GameGlobalInfo() },
        { GameProgress.Game2FirstHalf, new GameGlobalInfo() },
        { GameProgress.Game2SecondHalf, new GameGlobalInfo() },
        { GameProgress.Game3FirstHalf, new GameGlobalInfo() },
        { GameProgress.Game3SecondHalf, new GameGlobalInfo() },
        { GameProgress.Game4FirstHalf, new GameGlobalInfo() },
        { GameProgress.Game4SecondHalf, new GameGlobalInfo() },
        { GameProgress.Game5FirstHalf, new GameGlobalInfo() },
        { GameProgress.Game5SecondHalf, new GameGlobalInfo() },
        { GameProgress.Game5OvertimeFirstHalf, new GameGlobalInfo() },
        { GameProgress.Game5OvertimeSecondHalf, new GameGlobalInfo() },
    };

    private OrderedDictionary<GameProgress, string> _gameList = GameListBo5;

    public OrderedDictionary<GameProgress, string> GameList
    {
        get => _gameList;
        private set => SetPropertyWithAction(ref _gameList, value,
            _ => { SelectedGameProgress = value.GetAt(0).Key; });
    }

    private static OrderedDictionary<GameProgress, string> GameListBo5 => new()
    {
        { GameProgress.Game1FirstHalf, "Game1FirstHalf" },
        { GameProgress.Game1SecondHalf, "Game1SecondHalf" },
        { GameProgress.Game2FirstHalf, "Game2FirstHalf" },
        { GameProgress.Game2SecondHalf, "Game2SecondHalf" },
        { GameProgress.Game3FirstHalf, "Game3FirstHalf" },
        { GameProgress.Game3SecondHalf, "Game3SecondHalf" },
        { GameProgress.Game4FirstHalf, "Game4FirstHalf" },
        { GameProgress.Game4SecondHalf, "Game4SecondHalf" },
        { GameProgress.Game5FirstHalf, "Game5FirstHalf" },
        { GameProgress.Game5SecondHalf, "Game5SecondHalf" },
        { GameProgress.Game5OvertimeFirstHalf, "Game5OvertimeFirstHalf" },
        { GameProgress.Game5OvertimeSecondHalf, "Game5OvertimeSecondHalf" }
    };

    private static OrderedDictionary<GameProgress, string> GameListBo3 => new()
    {
        { GameProgress.Game1FirstHalf, "Game1FirstHalf" },
        { GameProgress.Game1SecondHalf, "Game1SecondHalf" },
        { GameProgress.Game2FirstHalf, "Game2FirstHalf" },
        { GameProgress.Game2SecondHalf, "Game2SecondHalf" },
        { GameProgress.Game3FirstHalf, "Game3FirstHalf" },
        { GameProgress.Game3SecondHalf, "Game3SecondHalf" },
        { GameProgress.Game3OvertimeFirstHalf, "Game3OvertimeFirstHalf" },
        { GameProgress.Game3OvertimeSecondHalf, "Game3OvertimeSecondHalf" }
    };

    public partial class GameGlobalInfo : ObservableObject
    {
        [ObservableProperty] private bool _isGameFinished;
        [ObservableProperty] private Camp? _homeTeamCamp;
        [ObservableProperty] private GameResult? _gameResult;
    }

    #endregion
}
