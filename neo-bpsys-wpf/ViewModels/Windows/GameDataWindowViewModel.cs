using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using Game = neo_bpsys_wpf.Core.Models.Game;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class GameDataWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public GameDataWindowViewModel()
#pragma warning restore CS8618
    {
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly FrontedWindowRuntimeSettings _settings = new();

    public GameDataWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        sharedDataService.IsBo3ModeChanged += (_, _) => OnPropertyChanged(nameof(IsBo3Mode));
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public FrontedWindowRuntimeSettings Settings => _settings;
}
