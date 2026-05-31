using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using Game = neo_bpsys_wpf.Core.Models.Game;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class CutSceneWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public CutSceneWindowViewModel()
#pragma warning restore CS8618
    {
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public CutSceneWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(settingsHostService.Settings.CutSceneWindowSettings))
                OnPropertyChanged(nameof(Settings));
        };
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public CutSceneWindowSettings Settings => _settingsHostService.Settings.CutSceneWindowSettings;
}
