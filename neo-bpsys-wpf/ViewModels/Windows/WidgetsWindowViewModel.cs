using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class WidgetsWindowViewModel : 
        ObservableRecipient, 
        IRecipient<NewGameMessage>, 
        IRecipient<DesignModeChangedMessage>,
        IRecipient<PropertyChangedMessage<bool>>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public WidgetsWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

        private readonly ISharedDataService _sharedDataService;

        public WidgetsWindowViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            CurrentBanLockImage = ImageHelper.GetUiImageSource("CurrentBanLock");
            GlobalBanLockImage = ImageHelper.GetUiImageSource("GlobalBanLock");
            IsActive = true;
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

        public ImageSource? CurrentBanLockImage { get; private set; }
        public ImageSource? GlobalBanLockImage { get; private set; }

        public Game CurrentGame => _sharedDataService.CurrentGame;

        public ObservableCollection<bool> CanCurrentSurBanned => _sharedDataService.CanCurrentSurBanned;
        public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBanned;
        public ObservableCollection<bool> CanGlobalSurBanned => _sharedDataService.CanGlobalSurBanned;
        public ObservableCollection<bool> CanGlobalHunBanned => _sharedDataService.CanGlobalHunBanned;
    }
}
