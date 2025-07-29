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

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class GameDataWindowViewModel :
    ViewModelBase,
    IRecipient<NewGameMessage>,
    IRecipient<DesignModeChangedMessage>,
    IRecipient<PropertyChangedMessage<bool>>,
    IRecipient<MemberPropertyChangedMessage>,
    IRecipient<SettingsChangedMessage>
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public GameDataWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    [ObservableProperty]
    private bool _isDesignMode;

    public GameDataWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        IsBo3Mode = _sharedDataService.IsBo3Mode;
    }
    
    public ImageSource? BgImage => Settings.BgImageUri == null
        ? ImageHelper.GetUiImageSource("gameData")
        : new BitmapImage(new Uri(Settings.BgImageUri));

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public void Receive(NewGameMessage message)
    {
        OnPropertyChanged(nameof(CurrentGame));
    }

    public void Receive(DesignModeChangedMessage message)
    {
        if (IsDesignMode != message.IsDesignMode)
            IsDesignMode = message.IsDesignMode;
    }

    [ObservableProperty]
    private bool _isBo3Mode;
    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.PropertyName == nameof(ISharedDataService.IsBo3Mode))
        {
            IsBo3Mode = message.NewValue;
        }
    }

    public void Receive(MemberPropertyChangedMessage message)
    {
        OnPropertyChanged(nameof(CurrentGame));
    }
    
    public GameDataWindowSettings Settings => _settingsHostService.Settings.GameDataWindowSettings;

    public void Receive(SettingsChangedMessage message)
    {
        if(message.WindowType == FrontWindowType.GameDataWindow)
            OnPropertyChanged(nameof(Settings));
    }
}
