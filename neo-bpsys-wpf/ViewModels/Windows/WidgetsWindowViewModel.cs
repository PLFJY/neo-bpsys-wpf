using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using System.Collections.ObjectModel;
using Game = neo_bpsys_wpf.Core.Models.Game;
using WidgetsWindowSettings = neo_bpsys_wpf.Core.Models.WidgetsWindowSettings;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class WidgetsWindowViewModel :
    ViewModelBase,
    IRecipient<DesignerModeChangedMessage>
{
    public WidgetsWindowViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty] private bool _isDesignerMode;

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

    public void Receive(DesignerModeChangedMessage message)
    {
        if (message.FrontedWindowId == FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.WidgetsWindow) &&
            IsDesignerMode != message.IsDesignerMode)
            IsDesignerMode = message.IsDesignerMode;
    }

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public ObservableCollection<bool> CanCurrentSurBanned => _sharedDataService.CanCurrentSurBannedList;
    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBannedList;
    public WidgetsWindowSettings Settings => _settingsHostService.Settings.WidgetsWindowSettings;
}