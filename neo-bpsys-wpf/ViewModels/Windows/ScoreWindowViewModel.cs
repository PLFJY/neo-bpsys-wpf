using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class ScoreWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public ScoreWindowViewModel()
#pragma warning restore CS8618
    {
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public ScoreWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentGame));
        };
        sharedDataService.IsBo3ModeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsBo3Mode));
            OnPropertyChanged(nameof(ScoreGlobalImage));
        };
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(settingsHostService.Settings.ScoreWindowSettings)) return;
            OnPropertyChanged(nameof(Settings));
            Settings.PropertyChanged += SettingsOnPropertyChanged;
        };
        Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.GlobalScoreBgImage) or nameof(Settings.GlobalScoreBgImageBo3))
        {
            OnPropertyChanged(nameof(ScoreGlobalImage));
        }
    }


    public ImageSource? ScoreGlobalImage => IsBo3Mode ? Settings.GlobalScoreBgImageBo3 : Settings.GlobalScoreBgImage;

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public Team HomeTeam => _sharedDataService.HomeTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    public ScoreWindowSettings Settings => _settingsHostService.Settings.ScoreWindowSettings;
}
