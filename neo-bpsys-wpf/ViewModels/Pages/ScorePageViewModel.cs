using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

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

        public ISharedDataService SharedDataService { get; }

        public ScorePageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }

        [RelayCommand]
        private void Escape4()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 5;
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private void Escape3()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 3;
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 1;
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private void Tie()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 2;
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 2;
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private void Out3()
        {
            SharedDataService.CurrentGame.SurTeam.Score.MinorPoints += 1;
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 3;
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private void Out4()
        {
            SharedDataService.CurrentGame.HunTeam.Score.MinorPoints += 5;
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private void Reset()
        {
            SharedDataService.MainTeam.Score = new();
            SharedDataService.AwayTeam.Score = new();
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        private void CaculateMajorPoint()
        {
            if (SharedDataService.MainTeam.Score.MinorPoints == SharedDataService.AwayTeam.Score.MinorPoints)
            {
                SharedDataService.MainTeam.Score.Tie++;
                SharedDataService.AwayTeam.Score.Tie++;
            }
            else if (SharedDataService.MainTeam.Score.MinorPoints > SharedDataService.AwayTeam.Score.MinorPoints)
            {
                SharedDataService.MainTeam.Score.Win++;
                SharedDataService.AwayTeam.Score.Lose++;
            }
            else
            {
                SharedDataService.MainTeam.Score.Lose++;
                SharedDataService.AwayTeam.Score.Win++;
            }
            SharedDataService.MainTeam.Score.MinorPoints = 0;
            SharedDataService.AwayTeam.Score.MinorPoints = 0;
            OnPropertyChanged(string.Empty);
        }

        public Dictionary<GameProgress, string> GameList { get; } =
            new Dictionary<GameProgress, string>()
            {
                { GameProgress.Game1FirstHalf, "BO1上半" },
                { GameProgress.Game1SecondHalf, "BO1下半" },
                { GameProgress.Game2FirstHalf, "BO2上半" },
                { GameProgress.Game2SecondHalf, "BO2下半" },
                { GameProgress.Game3FirstHalf, "BO3上半" },
                { GameProgress.Game3SecondHalf, "BO3下半" },
                { GameProgress.Game3ExtraFirstHalf, "BO3加赛上半" },
                { GameProgress.Game3ExtraSecondHalf, "BO3加赛下半" },
                { GameProgress.Game4FirstHalf, "BO4上半" },
                { GameProgress.Game4SecondHalf, "BO4下半" },
                { GameProgress.Game5FirstHalf, "BO5上半" },
                { GameProgress.Game5SecondHalf, "BO5下半" },
                { GameProgress.Game5ExtraFirstHalf, "BO5加赛上半" },
                { GameProgress.Game5ExtraSecondHalf, "BO5加赛下半" },
            };
    }
}
