using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class ScoreWindowViewModel :
    ViewModelBase,
    IRecipient<DesignerModeChangedMessage>,
    IRecipient<PropertyChangedMessage<int>>
{
    public ScoreWindowViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public ScoreWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        sharedDataService.IsBo3ModeChanged += (_, _) => OnPropertyChanged(nameof(IsBo3Mode));
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(settingsHostService.Settings.ScoreWindowSettings))
                OnPropertyChanged(nameof(Settings));
        };
    }

    [ObservableProperty] private bool _isDesignerMode;

    [ObservableProperty] private int _totalMainGameScore;

    [ObservableProperty] private int _totalAwayGameScore;

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public void Receive(DesignerModeChangedMessage message)
    {
        if (message.FrontedWindowId == FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreGlobalWindow) ||
            message.FrontedWindowId == FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreHunWindow) ||
            message.FrontedWindowId == FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreSurWindow) ||
            message.FrontedWindowId == FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.ScoreWindow)
            && IsDesignerMode != message.IsDesignerMode)
            IsDesignerMode = message.IsDesignerMode;
    }

    public void Receive(PropertyChangedMessage<int> message)
    {
        switch (message.PropertyName)
        {
            case nameof(TotalMainGameScore):
                TotalMainGameScore = message.NewValue;
                break;
            case nameof(TotalAwayGameScore):
                TotalAwayGameScore = message.NewValue;
                break;
        }
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public Team MainTeam => _sharedDataService.MainTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    public ScoreWindowSettings Settings => _settingsHostService.Settings.ScoreWindowSettings;
}