using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class WidgetsWindowViewModel : ObservableRecipient, IRecipient<NewGameMessage>, IRecipient<PropertyChangedMessage<bool>>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public WidgetsWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

        public ISharedDataService SharedDataService { get; }

        public WidgetsWindowViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            IsActive = true;
        }

        public void Receive(NewGameMessage message)
        {
            if (message.IsNewGameCreated)
            {
                OnPropertyChanged(nameof(CurrentGame));
            }
        }

        public void Receive(PropertyChangedMessage<bool> message)
        {
            if (message.PropertyName == nameof(IsDesignMode) && IsDesignMode != message.NewValue)
            {
                IsDesignMode = message.NewValue;
            }
        }

        public Game CurrentGame => SharedDataService.CurrentGame;
    }
}
