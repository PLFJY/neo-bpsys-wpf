using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

public class GameDataPageViewModel : ViewModelBase
{
    public GameDataPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    public GameDataPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SurPlayerList));
            OnPropertyChanged(nameof(HunPlayer));
        };
    }

    public IReadOnlyCollection<Player> SurPlayerList => _sharedDataService.CurrentGame.SurPlayerList;

    public Player HunPlayer => _sharedDataService.CurrentGame.HunPlayer;
}