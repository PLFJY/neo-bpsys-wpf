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
        GameProgresses GameProgress { get; set; }
        List<Character> CharacterList { get; set; }
        List<string> SurNameList { get; set; }
        List<string> HunNameList { get; set; }
    }
}
