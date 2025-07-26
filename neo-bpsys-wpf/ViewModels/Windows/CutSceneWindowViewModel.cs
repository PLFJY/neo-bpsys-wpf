using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Abstractions.ViewModels;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class CutSceneWindowViewModel :
    ViewModelBase,
    IRecipient<NewGameMessage>,
    IRecipient<DesignModeChangedMessage>,
    IRecipient<PropertyChangedMessage<bool>>,
    IRecipient<SettingsChangedMessage>
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public CutSceneWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
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

        IsTraitVisible = _sharedDataService.IsTraitVisible;
        _isBo3Mode = _sharedDataService.IsBo3Mode;
    }

    public void Receive(NewGameMessage message)
    {
        if (message.IsNewGameCreated)
        {
            OnPropertyChanged(nameof(CurrentGame));
        }
    }

    public void Receive(DesignModeChangedMessage message)
    {
        if (IsDesignMode != message.IsDesignMode)
            IsDesignMode = message.IsDesignMode;
    }

    [ObservableProperty] private bool _isBo3Mode;

    public void Receive(PropertyChangedMessage<bool> message)
    {
        switch (message.PropertyName)
        {
            case nameof(ISharedDataService.IsTraitVisible):
                IsTraitVisible = message.NewValue;
                break;
            case nameof(ISharedDataService.IsBo3Mode):
                IsBo3Mode = message.NewValue;
                break;
        }
    }

    public ImageSource? BgImage => Settings.BgUri == null
        ? ImageHelper.GetUiImageSource("cutScene")
        : new BitmapImage(new Uri(Settings.BgUri));

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

    [ObservableProperty] private bool _isTraitVisible;

    public CutSceneWindowSettings Settings => _settingsHostService.Settings.CutSceneWindowSettings;

    public void Receive(SettingsChangedMessage message)
    {
        if (message.WindowType == FrontWindowType.CutSceneWindow)
            OnPropertyChanged(nameof(Settings));
    }
}