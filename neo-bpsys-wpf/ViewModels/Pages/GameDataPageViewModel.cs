using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class GameDataPageViewModel : ViewModelBase
{
#pragma warning disable CS8618 
    public GameDataPageViewModel()
#pragma warning restore CS8618 
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISmartBpService _smartBpService;

    public GameDataPageViewModel(ISharedDataService sharedDataService, ISmartBpService smartBpService)
    {
        _sharedDataService = sharedDataService;
        _smartBpService = smartBpService;
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SurPlayerList));
            OnPropertyChanged(nameof(HunPlayer));
        };
    }

    public IReadOnlyCollection<Player> SurPlayerList => _sharedDataService.CurrentGame.SurPlayerList;

    public Player HunPlayer => _sharedDataService.CurrentGame.HunPlayer;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AutoFillGameDataAsync()
    {
        await _smartBpService.AutoFillGameDataAsync();
    }
}
