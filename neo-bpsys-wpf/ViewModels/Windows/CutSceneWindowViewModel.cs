using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using System.Windows.Media;
using Game = neo_bpsys_wpf.Core.Models.Game;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class CutSceneWindowViewModel :
    ViewModelBase,
    IRecipient<DesignModeChangedMessage>
{
    public CutSceneWindowViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    [ObservableProperty] private bool _isDesignMode;

    public CutSceneWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.IsTraitVisibleChanged += (_, _) => OnPropertyChanged(nameof(IsTraitVisible));
        sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(CurrentGame));
        sharedDataService.IsBo3ModeChanged += (_, _) => OnPropertyChanged(nameof(IsBo3Mode));
        settingsHostService.SettingsChanged += (_, _) => OnPropertyChanged(nameof(Settings));
        settingsHostService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(settingsHostService.Settings.CutSceneWindowSettings))
                OnPropertyChanged(nameof(Settings));
        };
    }

    public void Receive(DesignModeChangedMessage message)
    {
        if (message.FrontWindowType == FrontWindowType.CutSceneWindow && IsDesignMode != message.IsDesignMode)
            IsDesignMode = message.IsDesignMode;
    }

    public bool IsBo3Mode => _sharedDataService.IsBo3Mode;

    //talent imageSource

    //Sur
    public ImageSource? BorrowedTimeImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Sur, "回光返照", Settings.IsBlackTalentAndTraitEnable);

    public ImageSource? FlywheelEffectImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Sur, "飞轮效应", Settings.IsBlackTalentAndTraitEnable);

    public ImageSource? KneeJerkReflexImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Sur, "膝跳反射", Settings.IsBlackTalentAndTraitEnable);

    public ImageSource? TideTurnerImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Sur, "化险为夷", Settings.IsBlackTalentAndTraitEnable);

    //Hun
    public ImageSource? ConfinedSpaceImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Hun, "禁闭空间", Settings.IsBlackTalentAndTraitEnable);

    public ImageSource? DetentionImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Hun, "挽留", Settings.IsBlackTalentAndTraitEnable);

    public ImageSource? InsolenceImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Hun, "张狂", Settings.IsBlackTalentAndTraitEnable);

    public ImageSource? TrumpCardImageSource =>
        ImageHelper.GetTalentImageSource(Camp.Hun, "底牌", Settings.IsBlackTalentAndTraitEnable);

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public bool IsTraitVisible => _sharedDataService.IsTraitVisible;

    public CutSceneWindowSettings Settings => _settingsHostService.Settings.CutSceneWindowSettings;
}