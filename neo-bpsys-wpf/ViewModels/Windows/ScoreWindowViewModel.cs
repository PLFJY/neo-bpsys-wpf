using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class ScoreWindowViewModel :
    ViewModelBase,
    IRecipient<NewGameMessage>,
    IRecipient<DesignModeChangedMessage>,
    IRecipient<PropertyChangedMessage<int>>,
    IRecipient<SettingsChangedMessage>,
    IRecipient<PropertyChangedMessage<bool>>
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public ScoreWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public ScoreWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        IsBo3Mode = _sharedDataService.IsBo3Mode;
    }

    [ObservableProperty] private bool _isDesignMode;

    [ObservableProperty] private int _totalMainMinorPoint;

    [ObservableProperty] private int _totalAwayMinorPoint;

    [ObservableProperty] private bool _isBo3Mode;

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

    public void Receive(PropertyChangedMessage<int> message)
    {
        switch (message.PropertyName)
        {
            case nameof(TotalMainMinorPoint):
                TotalMainMinorPoint = message.NewValue;
                break;
            case nameof(TotalAwayMinorPoint):
                TotalAwayMinorPoint = message.NewValue;
                break;
        }
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public Team MainTeam => _sharedDataService.MainTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    public ScoreWindowSettings Settings => _settingsHostService.Settings.ScoreWindowSettings;

    public ImageSource? ScoreGlobalImage =>
        IsBo3Mode
            ? Settings.GlobalScoreBgImageUri == null
                ? ImageHelper.GetUiImageSource("scoreGlobal")
                : new BitmapImage(new Uri(Settings.GlobalScoreBgImageUri))
            : Settings.GlobalScoreBgImageUriBo3 == null
                ? ImageHelper.GetUiImageSource("scoreGlobal_Bo3")
                : new BitmapImage(new Uri(Settings.GlobalScoreBgImageUriBo3));

    public ImageSource? ScoreSurImage => Settings.SurScoreBgImageUri == null
        ? ImageHelper.GetUiImageSource("scoreSur")
        : new BitmapImage(new Uri(Settings.SurScoreBgImageUri));

    public ImageSource? ScoreHunImage => Settings.HunScoreBgImageUri == null
        ? ImageHelper.GetUiImageSource("scoreHun")
        : new BitmapImage(new Uri(Settings.HunScoreBgImageUri));

    public void Receive(SettingsChangedMessage message)
    {
        if (message.WindowType == FrontWindowType.ScoreGlobalWindow)
            OnPropertyChanged(nameof(Settings));
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.PropertyName == nameof(ISharedDataService.IsBo3Mode))
        {
            IsBo3Mode = message.NewValue;
        }
    }
}