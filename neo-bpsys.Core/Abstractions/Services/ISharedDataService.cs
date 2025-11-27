using System.Collections.ObjectModel;
using neo_bpsys.Core.Enums;
using neo_bpsys.Core.Models;

namespace neo_bpsys.Core.Abstractions.Services;

public interface ISharedDataService
{
    Team MainTeam { get; set; }
    Team AwayTeam { get; set; }
    Game CurrentGame { get; set; }
    Dictionary<string, Character> CharacterDict { get; }
    Dictionary<string, Character> SurCharaList { get; }
    Dictionary<string, Character> HunCharaList { get; }
    string RemainingSeconds { get; set; }
    bool IsBo3Mode { get; set; }
    double GlobalScoreTotalMargin { get; set; }
    void TimerStart(int? seconds);
    void TimerStop();
    event EventHandler? CurrentGameChanged;
    event EventHandler? IsBo3ModeChanged;
    event EventHandler? CountDownValueChanged;
}
