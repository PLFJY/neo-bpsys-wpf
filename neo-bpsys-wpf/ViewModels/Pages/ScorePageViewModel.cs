using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class ScorePageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ScorePageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public readonly ISharedDataService _sharedDataService;
        private readonly IFrontService _frontService;

        public ScorePageViewModel(ISharedDataService sharedDataService, IFrontService frontService)
        {
            _sharedDataService = sharedDataService;
            _frontService = frontService;
            GameList = GameListBo5;
        }

        public Team MainTeam => _sharedDataService.MainTeam;
        public Team AwayTeam => _sharedDataService.AwayTeam;


        #region 比分控制
        [RelayCommand]
        private void Escape4()
        {
            _sharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 5;
        }

        [RelayCommand]
        private void Escape3()
        {
            _sharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 3;
            _sharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 1;
        }

        [RelayCommand]
        private void Tie()
        {
            _sharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 2;
            _sharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 2;
        }

        [RelayCommand]
        private void Out3()
        {
            _sharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 1;
            _sharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 3;
        }

        [RelayCommand]
        private void Out4()
        {
            _sharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 5;
        }

        [RelayCommand]
        private void Reset()
        {
            _sharedDataService.MainTeam.Score = new();
            _sharedDataService.AwayTeam.Score = new();
        }

        [RelayCommand]
        private void ResetMinorPoint()
        {
            _sharedDataService.MainTeam.Score.MinorPoints = 0;
            _sharedDataService.AwayTeam.Score.MinorPoints = 0;
        }

        [RelayCommand]
        private void CaculateMajorPoint()
        {
            if (_sharedDataService.MainTeam.Score.MinorPoints == _sharedDataService.AwayTeam.Score.MinorPoints)
            {
                _sharedDataService.MainTeam.Score.Tie++;
                _sharedDataService.AwayTeam.Score.Tie++;
            }
            else if (_sharedDataService.MainTeam.Score.MinorPoints > _sharedDataService.AwayTeam.Score.MinorPoints)
            {
                _sharedDataService.MainTeam.Score.Win++;
                _sharedDataService.AwayTeam.Score.Lose++;
            }
            else
            {
                _sharedDataService.MainTeam.Score.Lose++;
                _sharedDataService.AwayTeam.Score.Win++;
            }
            _sharedDataService.MainTeam.Score.MinorPoints = 0;
            _sharedDataService.AwayTeam.Score.MinorPoints = 0;
            OnPropertyChanged(string.Empty);
        }
        #endregion

        #region 分数统计
        [ObservableProperty]
        private bool _isBo3Mode = false;

        private GameProgress _selectedGameProgress = GameProgress.Game1FirstHalf;
        public GameProgress SelectedGameProgress
        {
            get => _selectedGameProgress;
            set
            {
                _selectedGameProgress = value;
                MainTeamCamp = _gameGlobalInfoRecord[_selectedGameProgress].MainTeamCamp;
                IsGameFinished = _gameGlobalInfoRecord[_selectedGameProgress].IsGameFinished;
                SelectedGameResult = _gameGlobalInfoRecord[_selectedGameProgress].GameResult;
                OnPropertyChanged();
            }
        }
        [ObservableProperty]
        private int _selectedIndex = 0;

        private bool _isGameFinished;
        public bool IsGameFinished
        {
            get => _isGameFinished;
            set
            {
                _isGameFinished = value;
                if (!_isGameFinished)
                {
                    _frontService.SetGlobalScoreToBar(nameof(ISharedDataService.MainTeam), SelectedGameProgress);
                    _frontService.SetGlobalScoreToBar(nameof(ISharedDataService.AwayTeam), SelectedGameProgress);
                }
                _gameGlobalInfoRecord[_selectedGameProgress].IsGameFinished = _isGameFinished;
                OnPropertyChanged();
            }
        }

        private Camp? _mainTeamCamp;

        public Camp? MainTeamCamp
        {
            get => _mainTeamCamp;
            set
            {
                _mainTeamCamp = value;
                SyncGlobalScore();
                _gameGlobalInfoRecord[_selectedGameProgress].MainTeamCamp = _mainTeamCamp;
                OnPropertyChanged();
            }
        }

        public GameResult? _selectedGameResult;

        public GameResult? SelectedGameResult
        {
            get => _selectedGameResult;
            set
            {
                _selectedGameResult = value;
                SyncGlobalScore();
                _gameGlobalInfoRecord[_selectedGameProgress].GameResult = _selectedGameResult;
                OnPropertyChanged();
            }
        }

        private void SyncGlobalScore()
        {
            if(!_isGameFinished)
            {
                _frontService.SetGlobalScoreToBar(nameof(ISharedDataService.MainTeam), SelectedGameProgress);
                _frontService.SetGlobalScoreToBar(nameof(ISharedDataService.AwayTeam), SelectedGameProgress);
                return;
            }

            if (MainTeamCamp == null) return;

            if (_selectedGameResult != null)
            {
                int surScore = 0;
                int hunScore = 0;
                switch (_selectedGameResult)
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
                    default:
                        break;
                }
                if (MainTeamCamp == Camp.Sur)
                {
                    _frontService.SetGlobalScore(nameof(ISharedDataService.MainTeam), SelectedGameProgress, Camp.Sur, surScore);
                    _frontService.SetGlobalScore(nameof(ISharedDataService.AwayTeam), SelectedGameProgress, Camp.Hun, hunScore);
                }
                if (MainTeamCamp == Camp.Hun)
                {
                    _frontService.SetGlobalScore(nameof(ISharedDataService.MainTeam), SelectedGameProgress, Camp.Hun, hunScore);
                    _frontService.SetGlobalScore(nameof(ISharedDataService.AwayTeam), SelectedGameProgress, Camp.Sur, surScore);
                }
            }
        }

        [RelayCommand]
        private void NextGame()
        {
            int? index = 0;
            index = GameList
                .Select((pair, index) => new { Pair = pair, Index = index })
                .FirstOrDefault(pair => pair.Pair.Value == SelectedGameProgress)?.Index;
            index++;
            if (index != null)
            {
                int i = (int)index;
                SelectedIndex = i;
            }
        }

        [RelayCommand]
        private void SwitchGameType()
        {
            IsBo3Mode = !IsBo3Mode;
            if (!IsBo3Mode)
            {
                GameList = GameListBo5;
            }
            else
            {
                GameList = GameListBo3;
            }
            _frontService.SwitchGameType(IsBo3Mode);
            SelectedIndex = 0;
        }

        private readonly Dictionary<GameProgress, GameGlobalInfo> _gameGlobalInfoRecord = new()
        {
            {GameProgress.Game1FirstHalf, new()},
            {GameProgress.Game1SecondHalf, new()},
            {GameProgress.Game2FirstHalf, new()},
            {GameProgress.Game2SecondHalf, new()},
            {GameProgress.Game3FirstHalf, new()},
            {GameProgress.Game3SecondHalf, new()},
            {GameProgress.Game4FirstHalf, new()},
            {GameProgress.Game4SecondHalf, new()},
            {GameProgress.Game5FirstHalf, new()},
            {GameProgress.Game5SecondHalf, new()},
            {GameProgress.Game5ExtraFirstHalf, new()},
            {GameProgress.Game5ExtraSecondHalf, new()},
        };

        [ObservableProperty]
        private Dictionary<string, GameProgress> _gameList;

        public static Dictionary<string, GameProgress> GameListBo5 => new()
        {
            { "第1局上半", GameProgress.Game1FirstHalf },
            { "第1局下半", GameProgress.Game1SecondHalf },
            { "第2局上半", GameProgress.Game2FirstHalf },
            { "第2局下半", GameProgress.Game2SecondHalf },
            { "第3局上半", GameProgress.Game3FirstHalf },
            { "第3局下半", GameProgress.Game3SecondHalf },
            { "第4局上半", GameProgress.Game4FirstHalf },
            { "第4局下半", GameProgress.Game4SecondHalf },
            { "第5局上半", GameProgress.Game5FirstHalf },
            { "第5局下半", GameProgress.Game5SecondHalf },
            { "第5局加赛上半", GameProgress.Game5ExtraFirstHalf },
            { "第5局加赛下半", GameProgress.Game5ExtraSecondHalf },
        };

        public static Dictionary<string, GameProgress> GameListBo3 => new()
        {
            { "第1局上半", GameProgress.Game1FirstHalf },
            { "第1局下半", GameProgress.Game1SecondHalf },
            { "第2局上半", GameProgress.Game2FirstHalf },
            { "第2局下半", GameProgress.Game2SecondHalf },
            { "第3局上半", GameProgress.Game3FirstHalf },
            { "第3局下半", GameProgress.Game3SecondHalf },
            { "第3局加赛上半", GameProgress.Game3ExtraFirstHalf },
            { "第3局加赛下半", GameProgress.Game3ExtraSecondHalf },
        };

        public class GameGlobalInfo()
        {
            public bool IsGameFinished { get; set; }
            public Camp? MainTeamCamp { get; set; }
            public GameResult? GameResult { get; set; }
        }

        #endregion
    }
}
