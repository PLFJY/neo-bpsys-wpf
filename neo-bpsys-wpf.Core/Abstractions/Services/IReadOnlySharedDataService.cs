using System.Collections.ObjectModel;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IReadOnlySharedDataService
{
    Team MainTeam { get; }
    Team AwayTeam { get; }
    Game CurrentGame { get; }
    IReadOnlyDictionary<string, Character> CharacterList { get; }
    IReadOnlyDictionary<string, Character> SurCharaList { get; }
    IReadOnlyDictionary<string, Character> HunCharaList { get; }
    ReadOnlyObservableCollection<bool> CanCurrentSurBanned { get; }
    ReadOnlyObservableCollection<bool> CanCurrentHunBanned { get; }
    ReadOnlyObservableCollection<bool> CanGlobalSurBanned { get; }
    ReadOnlyObservableCollection<bool> CanGlobalHunBanned { get; }
    bool IsTraitVisible { get; }
    string RemainingSeconds { get; }
    bool IsBo3Mode { get; }
    double GlobalScoreTotalMargin { get; }
}