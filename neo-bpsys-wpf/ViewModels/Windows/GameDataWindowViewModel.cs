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
    private readonly ISettingsHostService _settingsHostService;

    public GameDataWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        sharedDataService.IsBo3ModeChanged += (_, _) => OnPropertyChanged(nameof(IsBo3Mode));
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(settingsHostService.Settings.GameDataWindowSettings))
                OnPropertyChanged(nameof(Settings));
        };
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public GameDataWindowSettings Settings => _settingsHostService.Settings.GameDataWindowSettings;
}
