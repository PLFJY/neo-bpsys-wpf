using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using Game = neo_bpsys_wpf.Core.Models.Game;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class CutSceneWindowViewModel :
    ViewModelBase,
    IRecipient<DesignerModeChangedMessage>
{
#pragma warning disable CS8618 
    public CutSceneWindowViewModel()
#pragma warning restore CS8618 
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    [ObservableProperty] private bool _isDesignerMode;

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

    public void Receive(DesignerModeChangedMessage message)
    {
        if (message.FrontedWindowId == FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.CutSceneWindow) && IsDesignerMode != message.IsDesignerMode)
            IsDesignerMode = message.IsDesignerMode;
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public CutSceneWindowSettings Settings => _settingsHostService.Settings.CutSceneWindowSettings;
}
