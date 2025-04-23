using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Services
{
    public interface ISharedDataService
    {
        Team MainTeam { get; set; }
        Team AwayTeam { get; set; }
        Team CurrentSurTeam { get; set; }
        Team CurrentHunTeam { get; set; }
        Game CurrentGame { get; set; }
        GameProgress CurrentGameProgress { get; set; }
        Dictionary<string, Character> CharacterList { get; set; }
        Dictionary<string, Character> SurCharaList { get; set; }
        Dictionary<string, Character> HunCharaList { get; set; }
    }
}
