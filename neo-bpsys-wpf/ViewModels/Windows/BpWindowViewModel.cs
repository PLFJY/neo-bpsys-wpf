using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using Game = neo_bpsys_wpf.Core.Models.Game;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class BpWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public BpWindowViewModel()
#pragma warning restore CS8618

    {
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public BpWindowSettings Settings => _settingsHostService.Settings.BpWindowSettings;

    public BpWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        sharedDataService.IsBo3ModeChanged += (_, e) => OnPropertyChanged(nameof(IsBo3Mode));
        sharedDataService.CountDownValueChanged += (sender, _) => OnPropertyChanged(nameof(RemainingSeconds));
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(settingsHostService.Settings.BpWindowSettings))
                OnPropertyChanged(nameof(Settings));
        };
    }

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public string RemainingSeconds => _sharedDataService.RemainingSeconds;

    public ObservableCollection<bool> CanCurrentSurBanned => _sharedDataService.CanCurrentSurBannedList;
    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBannedList;
    public ObservableCollection<bool> CanGlobalSurBanned => _sharedDataService.CanGlobalSurBannedList;
    public ObservableCollection<bool> CanGlobalHunBanned => _sharedDataService.CanGlobalHunBannedList;
}
