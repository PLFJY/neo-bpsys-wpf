using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.ComponentModel;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Models;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class ScorePageViewModel : ViewModelBase, IRecipient<PropertyChangedMessage<bool>>
{
#pragma warning disable CS8618
    public ScorePageViewModel()
#pragma warning restore CS8618
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IFrontedWindowService _frontedWindowService;

    public ScorePageViewModel(ISharedDataService sharedDataService, IFrontedWindowService frontedWindowService)
    {
        _sharedDataService = sharedDataService;
        _frontedWindowService = frontedWindowService;
        _isBo3Mode = _sharedDataService.IsBo3Mode;
        _sharedDataService.CurrentGame.TeamSwapped += SyncHomeTeamCampFromGlobal;
        _sharedDataService.CurrentGame.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(Game.GameProgress) &&
                _sharedDataService.CurrentGame.GameProgress > GameProgress.Free)
            {
                SelectedGameProgress = _sharedDataService.CurrentGame.GameProgress;
            }
        };
    }

    private void SyncHomeTeamCampFromGlobal(object? sender, EventArgs args)
    {
        HomeTeamCamp = _sharedDataService.HomeTeam.Camp == Camp.Sur ? Camp.Sur : Camp.Hun;
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
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 5;
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free) return;
        IsGameFinished = true;
        SelectedGameResult = GameResult.Escape4;
    }

    [RelayCommand]
    private void Escape3()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 3;
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 1;
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free) return;
        IsGameFinished = true;
        SelectedGameResult = GameResult.Escape3;
    }

    [RelayCommand]
    private void Tie()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 2;
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 2;
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free) return;
        IsGameFinished = true;
        SelectedGameResult = GameResult.Tie;
    }

    [RelayCommand]
    private void Out3()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 1;
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 3;
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free) return;
        IsGameFinished = true;
        SelectedGameResult = GameResult.Out3;
    }

    [RelayCommand]
    private void Out4()
    {
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 5;
        if (_sharedDataService.CurrentGame.GameProgress <= GameProgress.Free) return;
        IsGameFinished = true;
        SelectedGameResult = GameResult.Out4;
    }

    [RelayCommand]
    private void Reset()
    {
        _sharedDataService.HomeTeam.ResetScore();
        _sharedDataService.AwayTeam.ResetScore();
    }

    [RelayCommand]
    private void ResetGameScore()
    {
        _sharedDataService.HomeTeam.Score.GameScores = 0;
        _sharedDataService.AwayTeam.Score.GameScores = 0;
    }

    [RelayCommand]
    private void CalculateMajorPoint()
    {
        if (_sharedDataService.HomeTeam.Score.GameScores == _sharedDataService.AwayTeam.Score.GameScores)
        {
            _sharedDataService.HomeTeam.Score.Tie++;
            _sharedDataService.AwayTeam.Score.Tie++;
        }
        else if (_sharedDataService.HomeTeam.Score.GameScores > _sharedDataService.AwayTeam.Score.GameScores)
        {
            _sharedDataService.HomeTeam.Score.Win++;
        }
        else
        {
            _sharedDataService.AwayTeam.Score.Win++;
        }

        _sharedDataService.HomeTeam.Score.GameScores = 0;
        _sharedDataService.AwayTeam.Score.GameScores = 0;
        OnPropertyChanged(string.Empty);
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
            UpdateTotalGameScore();
        });
    }

    private GameProgress _selectedGameProgress = GameProgress.Game1FirstHalf;

    public GameProgress SelectedGameProgress
    {
        get => _selectedGameProgress;
        set => SetPropertyWithAction(ref _selectedGameProgress, value, _ =>
        {
            var gameInfo = GameGlobalInfoRecord[value];
            IsGameFinished = gameInfo.IsGameFinished;
            HomeTeamCamp = gameInfo.HomeTeamCamp;
            SelectedGameResult = gameInfo.GameResult;
            SyncHomeTeamCampFromGlobal(this, EventArgs.Empty);

            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(HomeTeamCamp));
            OnPropertyChanged(nameof(SelectedGameResult));
            OnPropertyChanged(nameof(SelectedIndex));
        });
    }

    public int SelectedIndex => GameList.IndexOf(SelectedGameProgress);

    [RelayCommand]
    private void GlobalScoreUpdateToFront()
    {
        GameGlobalInfoRecord[SelectedGameProgress].IsGameFinished = IsGameFinished;
        if (!IsGameFinished)
        {
            _frontedWindowService.SetGlobalScoreToBar(TeamType.HomeTeam, SelectedGameProgress);
            _frontedWindowService.SetGlobalScoreToBar(TeamType.AwayTeam, SelectedGameProgress);
            UpdateTotalGameScore();
            return;
        }

        GameGlobalInfoRecord[SelectedGameProgress].HomeTeamCamp = HomeTeamCamp;
        GameGlobalInfoRecord[SelectedGameProgress].GameResult = SelectedGameResult;
        UpdateGlobalScore();
        UpdateTotalGameScore();
    }

    [ObservableProperty] private bool _isGameFinished;

    [ObservableProperty] private Camp? _homeTeamCamp = Camp.Sur;

    [ObservableProperty] private GameResult? _selectedGameResult;

    private void UpdateGlobalScore()
    {
        if (!IsGameFinished)
        {
            _frontedWindowService.SetGlobalScoreToBar(TeamType.HomeTeam, SelectedGameProgress);
            _frontedWindowService.SetGlobalScoreToBar(TeamType.AwayTeam, SelectedGameProgress);
            return;
        }

        if (HomeTeamCamp == null || SelectedGameResult == null) return;

        var surScore = 0;
        var hunScore = 0;
        switch (SelectedGameResult)
        {
            case GameResult.Escape4:
                surScore = 5;
                hunScore = 0;
                break;
            case GameResult.Escape3:
                surScore = 3;
                hunScore = 1;
                break;
            case GameResult.Tie:
                surScore = 2;
                hunScore = 2;
                break;
            case GameResult.Out3:
                surScore = 1;
                hunScore = 3;
                break;
            case GameResult.Out4:
                surScore = 0;
                hunScore = 5;
                break;
        }

        switch (HomeTeamCamp)
        {
            case Camp.Sur:
                _frontedWindowService.SetGlobalScore(TeamType.HomeTeam, SelectedGameProgress, Camp.Sur,
                    surScore);
                _frontedWindowService.SetGlobalScore(TeamType.AwayTeam, SelectedGameProgress, Camp.Hun,
                    hunScore);
                break;
            case Camp.Hun:
                _frontedWindowService.SetGlobalScore(TeamType.HomeTeam, SelectedGameProgress, Camp.Hun,
                    hunScore);
                _frontedWindowService.SetGlobalScore(TeamType.AwayTeam, SelectedGameProgress, Camp.Sur,
                    surScore);
                break;
            case null:
                break;
            default:
                throw new InvalidEnumArgumentException();
        }
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
        var totalMainGameScore = 0;
        var totalAwayGameScore = 0;
        foreach (var i in GameGlobalInfoRecord.Where(i => i.Value.IsGameFinished)
                     .TakeWhile(i => !IsBo3Mode || i.Key <= GameProgress.Game4SecondHalf))
        {
            switch (i.Value.GameResult)
            {
                case GameResult.Escape4:
                    if (i.Value.HomeTeamCamp == Camp.Sur)
                    {
                        totalMainGameScore += 5;
                        totalAwayGameScore += 0;
                    }

                    if (i.Value.HomeTeamCamp == Camp.Hun)
                    {
                        totalMainGameScore += 0;
                        totalAwayGameScore += 5;
                    }

                    break;
                case GameResult.Escape3:
                    switch (i.Value.HomeTeamCamp)
                    {
                        case Camp.Sur:
                            totalMainGameScore += 3;
                            totalAwayGameScore += 1;
                            break;
                        case Camp.Hun:
                            totalMainGameScore += 1;
                            totalAwayGameScore += 3;
                            break;
                    }

                    break;
                case GameResult.Tie:
                    totalMainGameScore += 2;
                    totalAwayGameScore += 2;
                    break;
                case GameResult.Out3:
                    switch (i.Value.HomeTeamCamp)
                    {
                        case Camp.Sur:
                            totalMainGameScore += 1;
                            totalAwayGameScore += 3;
                            break;
                        case Camp.Hun:
                            totalMainGameScore += 3;
                            totalAwayGameScore += 1;
                            break;
                        case null:
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }

                    break;
                case GameResult.Out4:
                    switch (i.Value.HomeTeamCamp)
                    {
                        case Camp.Sur:
                            totalMainGameScore += 0;
                            totalAwayGameScore += 5;
                            break;
                        case Camp.Hun:
                            totalMainGameScore += 5;
                            totalAwayGameScore += 0;
                            break;
                        case null:
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }

                    break;
                case null:
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<int>(this,
            nameof(ScoreWindowViewModel.TotalMainGameScore), 0, totalMainGameScore));
        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<int>(this,
            nameof(ScoreWindowViewModel.TotalAwayGameScore), 0, totalAwayGameScore));
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