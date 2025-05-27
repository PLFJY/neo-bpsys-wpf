using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class BpWindowViewModel : ObservableRecipient, IRecipient<NewGameMessage>, IRecipient<PropertyChangedMessage<bool>>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public BpWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

        public ISharedDataService SharedDataService { get; }

        public BpWindowViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            CurrentBanLockImage = ImageHelper.GetUiImageSource("CurrentBanLock");
            GlobalBanLockImage = ImageHelper.GetUiImageSource("GlobalBanLock");
            IsActive = true;
        }

        public void Receive(PropertyChangedMessage<bool> message)
        {
            if (message.PropertyName == nameof(IsDesignMode) && IsDesignMode != message.NewValue)
            {
                IsDesignMode = message.NewValue;
            }
        }

        public void Receive(NewGameMessage message)
        {
            if (message.IsNewGameCreated)
            {
                OnPropertyChanged(nameof(CurrentGame));
            }
        }

        public ImageSource? CurrentBanLockImage { get; private set; }
        public ImageSource? GlobalBanLockImage { get; private set; }

        public Game CurrentGame => SharedDataService.CurrentGame;
    }
}
