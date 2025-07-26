using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class WidgetsWindowViewModel :
    ViewModelBase,
    IRecipient<NewGameMessage>,
    IRecipient<DesignModeChangedMessage>,
    IRecipient<PropertyChangedMessage<bool>>,
    IRecipient<SettingsChangedMessage>
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public WidgetsWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty]
    private bool _isDesignMode;

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public WidgetsWindowViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        IsBo3Mode = _sharedDataService.IsBo3Mode;
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

    [ObservableProperty]
    private bool _isBo3Mode;

    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.PropertyName == nameof(ISharedDataService.IsBo3Mode))
        {
            IsBo3Mode = message.NewValue;
        }
    }

    public ImageSource? MapBpImage => Settings.MapBpBgUri == null
        ? ImageHelper.GetUiImageSource("mapBp")
        : new BitmapImage(new Uri(Settings.MapBpBgUri));

    public ImageSource? MapBpV2Image => Settings.MapBpV2BgUri == null
        ? ImageHelper.GetUiImageSource("mapBpV2")
        : new BitmapImage(new Uri(Settings.MapBpV2BgUri));

    public ImageSource? BpOverViewBgImage => Settings.BpOverviewBgUri == null
        ? ImageHelper.GetUiImageSource("bpOverview")
        : new BitmapImage(new Uri(Settings.BpOverviewBgUri));

    public ImageSource? CurrentBanLockImage => Settings.CurrentBanLockImageUri == null
        ? ImageHelper.GetUiImageSource("CurrentBanLock")
        : new BitmapImage(new Uri(Settings.CurrentBanLockImageUri));

    public ImageSource? GlobalBanLockImage => Settings.GlobalBanLockImageUri == null
        ? ImageHelper.GetUiImageSource("GlobalBanLock")
        : new BitmapImage(new Uri(Settings.GlobalBanLockImageUri));
    public ImageSource? MapBpV2PickingBorderSource => Settings.MapBpV2PickBorderImageUri == null
        ? ImageHelper.GetUiImageSource("pickingBorder")
        : new BitmapImage(new Uri(Settings.MapBpV2PickBorderImageUri));

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public ObservableCollection<bool> CanCurrentSurBanned => _sharedDataService.CanCurrentSurBanned;
    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBanned;
    public ObservableCollection<bool> CanGlobalSurBanned => _sharedDataService.CanGlobalSurBanned;
    public ObservableCollection<bool> CanGlobalHunBanned => _sharedDataService.CanGlobalHunBanned;
        
    public WidgetsWindowSettings Settings => _settingsHostService.Settings.WidgetsWindowSettings;

    public void Receive(SettingsChangedMessage message)
    {
        if(message.WindowType == FrontWindowType.WidgetsWindow)
            OnPropertyChanged(nameof(Settings));
    }
}