using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class ScorePageViewModel : ObservableObject
    {
        public ScorePageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
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
