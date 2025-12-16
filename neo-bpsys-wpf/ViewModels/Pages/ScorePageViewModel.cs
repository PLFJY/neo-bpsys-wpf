using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.ComponentModel;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class ScorePageViewModel : ViewModelBase, IRecipient<PropertyChangedMessage<bool>>
{
    public ScorePageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IFrontService _frontService;

    public ScorePageViewModel(ISharedDataService sharedDataService, IFrontService frontService)
    {
        _sharedDataService = sharedDataService;
        _frontService = frontService;
        _isBo3Mode = _sharedDataService.IsBo3Mode;
#if DEBUG
        IsDebugContentVisible = true;
#endif
    }

    [ObservableProperty] private bool _isDebugContentVisible;

    public Team MainTeam => _sharedDataService.MainTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    #region 比分控制

    [RelayCommand]
    private void Escape4()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 5;
    }

    [RelayCommand]
    private void Escape3()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 3;
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 1;
    }

    [RelayCommand]
    private void Tie()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 2;
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 2;
    }

    [RelayCommand]
    private void Out3()
    {
        _sharedDataService.CurrentGame.SurTeam.Score.GameScores += 1;
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 3;
    }

    [RelayCommand]
    private void Out4()
    {
        _sharedDataService.CurrentGame.HunTeam.Score.GameScores += 5;
    }

    [RelayCommand]
    private void Reset()
    {
        _sharedDataService.MainTeam.ResetScore();
        _sharedDataService.AwayTeam.ResetScore();
    }

    [RelayCommand]
    private void ResetGameScore()
    {
        _sharedDataService.MainTeam.Score.GameScores = 0;
        _sharedDataService.AwayTeam.Score.GameScores = 0;
    }

    [RelayCommand]
    private void CalculateMajorPoint()
    {
        if (_sharedDataService.MainTeam.Score.GameScores == _sharedDataService.AwayTeam.Score.GameScores)
        {
            _sharedDataService.MainTeam.Score.Tie++;
            _sharedDataService.AwayTeam.Score.Tie++;
        }
        else if (_sharedDataService.MainTeam.Score.GameScores > _sharedDataService.AwayTeam.Score.GameScores)
        {
            _sharedDataService.MainTeam.Score.Win++;
        }
        else
        {
            _sharedDataService.AwayTeam.Score.Win++;
        }

        _sharedDataService.MainTeam.Score.GameScores = 0;
        _sharedDataService.AwayTeam.Score.GameScores = 0;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private static void ManualControl()
    {
        App.Services.GetRequiredService<ScoreManualWindow>().ShowDialog();
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
            OnPropertyChanged(nameof(MainTeamCamp));
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
            MainTeamCamp = gameInfo.MainTeamCamp;
            SelectedGameResult = gameInfo.GameResult;

            OnPropertyChanged(nameof(IsGameFinished));
            OnPropertyChanged(nameof(MainTeamCamp));
            OnPropertyChanged(nameof(SelectedGameResult));
            OnPropertyChanged(nameof(SelectedIndex));
        });
    }

    public int SelectedIndex => GameList.IndexOf(SelectedGameProgress);

    [RelayCommand]
    private void NextGame()
    {
        var index = GameList.IndexOf(SelectedGameProgress);
        if (index < 7 && IsBo3Mode || index < 11 && !IsBo3Mode) // 防止在加赛下半场时点下一步会崩
        {
            SelectedGameProgress = GameList.ElementAt(index + 1).Key;
        };
    }

    [RelayCommand]
    private void GlobalScoreUpdateToFront()
    {
        GameGlobalInfoRecord[SelectedGameProgress].IsGameFinished = IsGameFinished;
        if (!IsGameFinished)
        {
            _frontService.SetGlobalScoreToBar(TeamType.HomeTeam, SelectedGameProgress);
            _frontService.SetGlobalScoreToBar(TeamType.AwayTeam, SelectedGameProgress);
            UpdateTotalGameScore();
            return;
        }

        GameGlobalInfoRecord[SelectedGameProgress].MainTeamCamp = MainTeamCamp;
        GameGlobalInfoRecord[SelectedGameProgress].GameResult = SelectedGameResult;
        UpdateGlobalScore();
        UpdateTotalGameScore();
    }

    [ObservableProperty] private bool _isGameFinished;

    [ObservableProperty] private Camp? _mainTeamCamp;

    [ObservableProperty] private GameResult? _selectedGameResult;

    private void UpdateGlobalScore()
    {
        if (!IsGameFinished)
        {
            _frontService.SetGlobalScoreToBar(TeamType.HomeTeam, SelectedGameProgress);
            _frontService.SetGlobalScoreToBar(TeamType.AwayTeam, SelectedGameProgress);
            return;
        }

        if (MainTeamCamp == null || SelectedGameResult == null) return;

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

        switch (MainTeamCamp)
        {
            case Camp.Sur:
                _frontService.SetGlobalScore(TeamType.HomeTeam, SelectedGameProgress, Camp.Sur,
                    surScore);
                _frontService.SetGlobalScore(TeamType.AwayTeam, SelectedGameProgress, Camp.Hun,
                    hunScore);
                break;
            case Camp.Hun:
                _frontService.SetGlobalScore(TeamType.HomeTeam, SelectedGameProgress, Camp.Hun,
                    hunScore);
                _frontService.SetGlobalScore(TeamType.AwayTeam, SelectedGameProgress, Camp.Sur,
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
        var _totalMainGameScore = 0;
        var _totalAwayGameScore = 0;
        foreach (var i in GameGlobalInfoRecord.Where(i => i.Value.IsGameFinished)
                     .TakeWhile(i => !IsBo3Mode || i.Key <= GameProgress.Game4SecondHalf))
        {
            switch (i.Value.GameResult)
            {
                case GameResult.Escape4:
                    if (i.Value.MainTeamCamp == Camp.Sur)
                    {
                        _totalMainGameScore += 5;
                        _totalAwayGameScore += 0;
                    }

                    if (i.Value.MainTeamCamp == Camp.Hun)
                    {
                        _totalMainGameScore += 0;
                        _totalAwayGameScore += 5;
                    }

                    break;
                case GameResult.Escape3:
                    switch (i.Value.MainTeamCamp)
                    {
                        case Camp.Sur:
                            _totalMainGameScore += 3;
                            _totalAwayGameScore += 1;
                            break;
                        case Camp.Hun:
                            _totalMainGameScore += 1;
                            _totalAwayGameScore += 3;
                            break;
                    }

                    break;
                case GameResult.Tie:
                    _totalMainGameScore += 2;
                    _totalAwayGameScore += 2;
                    break;
                case GameResult.Out3:
                    switch (i.Value.MainTeamCamp)
                    {
                        case Camp.Sur:
                            _totalMainGameScore += 1;
                            _totalAwayGameScore += 3;
                            break;
                        case Camp.Hun:
                            _totalMainGameScore += 3;
                            _totalAwayGameScore += 1;
                            break;
                        case null:
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }

                    break;
                case GameResult.Out4:
                    switch (i.Value.MainTeamCamp)
                    {
                        case Camp.Sur:
                            _totalMainGameScore += 0;
                            _totalAwayGameScore += 5;
                            break;
                        case Camp.Hun:
                            _totalMainGameScore += 5;
                            _totalAwayGameScore += 0;
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
            nameof(ScoreWindowViewModel.TotalMainGameScore), 0, _totalMainGameScore));
        WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<int>(this,
            nameof(ScoreWindowViewModel.TotalAwayGameScore), 0, _totalAwayGameScore));
    }

    public Dictionary<GameProgress, GameGlobalInfo> GameGlobalInfoRecord { get; } = new()
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
        { GameProgress.Game5ExtraFirstHalf, new GameGlobalInfo() },
        { GameProgress.Game5ExtraSecondHalf, new GameGlobalInfo() },
    };

    private OrderedDictionary<GameProgress, string> _gameList = GameListBo5;

    public OrderedDictionary<GameProgress, string> GameList
    {
        get => _gameList;
        private set => SetPropertyWithAction(ref _gameList, value, _ =>
        {
            SelectedGameProgress = value.ElementAt(0).Key;
        });
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
        { GameProgress.Game5ExtraFirstHalf, "Game5ExtraFirstHalf" },
        { GameProgress.Game5ExtraSecondHalf, "Game5ExtraSecondHalf" }
    };

    private static OrderedDictionary<GameProgress, string> GameListBo3 => new()
    {
        { GameProgress.Game1FirstHalf, "Game1FirstHalf" },
        { GameProgress.Game1SecondHalf, "Game1SecondHalf" },
        { GameProgress.Game2FirstHalf, "Game2FirstHalf" },
        { GameProgress.Game2SecondHalf, "Game2SecondHalf" },
        { GameProgress.Game3FirstHalf, "Game3FirstHalf" },
        { GameProgress.Game3SecondHalf, "Game3SecondHalf" },
        { GameProgress.Game3ExtraFirstHalf, "Game3ExtraFirstHalf" },
        { GameProgress.Game3ExtraSecondHalf, "Game3ExtraSecondHalf" }
    };

    public partial class GameGlobalInfo : ObservableObject
    {
        [ObservableProperty] private bool _isGameFinished;
        [ObservableProperty] private Camp? _mainTeamCamp;
        [ObservableProperty] private GameResult? _gameResult;
    }

    #endregion
}