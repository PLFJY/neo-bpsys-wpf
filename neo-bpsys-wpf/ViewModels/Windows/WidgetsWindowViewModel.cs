using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using Game = neo_bpsys_wpf.Core.Models.Game;
using WidgetsWindowSettings = neo_bpsys_wpf.Core.Models.WidgetsWindowSettings;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class WidgetsWindowViewModel :
    ViewModelBase,
    IRecipient<DesignModeChangedMessage>
{
    public WidgetsWindowViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty] private bool _isDesignMode;

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public WidgetsWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        sharedDataService.IsBo3ModeChanged += (_, _) => OnPropertyChanged(nameof(IsBo3Mode));
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(settingsHostService.Settings.WidgetsWindowSettings))
                OnPropertyChanged(nameof(Settings));
        };
    }
    public void Receive(DesignModeChangedMessage message)
    {
        if (message.FrontWindowType == FrontWindowType.WidgetsWindow && IsDesignMode != message.IsDesignMode)
            IsDesignMode = message.IsDesignMode;
    }

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public ObservableCollection<bool> CanCurrentSurBanned => _sharedDataService.CanCurrentSurBannedList;
    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBannedList;
    public WidgetsWindowSettings Settings => _settingsHostService.Settings.WidgetsWindowSettings;
}