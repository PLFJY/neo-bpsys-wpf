using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Services
{
    public interface ISharedDataService
    {
        Team MainTeam { get; set; }
        Team AwayTeam { get; set; }
        Game CurrentGame { get; set; }
        GameProgress CurrentGameProgress { get; set; }
        Dictionary<string, Character> CharacterList { get; set; }
        Dictionary<string, Character> SurCharaList { get; set; }
        Dictionary<string, Character> HunCharaList { get; set; }
        ObservableCollection<bool> CanCurrentSurBanned { get; set; }
        ObservableCollection<bool> CanCurrentHunBanned { get; set; }
        ObservableCollection<bool> CanGlobalSurBanned { get; set; }
        ObservableCollection<bool> CanGlobalHunBanned { get; set; }
        bool IsTraitVisible { get; set; }
        string RemainingSeconds { get; set; }
        void TimerStart(int seconds);
        void TimerStop();
    }
}
