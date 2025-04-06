using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class ScorePageViewModel : ObservableObject
    {
        public List<string> GameList { get; } =
        [
            "Game 1 First Half",
            "Game 1 Second Half",
            "Game 2 First Half",
            "Game 2 Second Half",
            "Game 3 First Half",
            "Game 3 Second Half",
            "Game 4 First Half",
            "Game 4 Second Half",
            "Game 5 First Half",
            "Game 5 Second Half",
            "Game 5 Extra First Half",
            "Game 5 Extra Second Half"
            ];
    }
}
