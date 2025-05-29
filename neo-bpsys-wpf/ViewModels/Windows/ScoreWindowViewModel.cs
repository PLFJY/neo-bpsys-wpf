using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class ScoreWindowViewModel : ObservableRecipient, IRecipient<NewGameMessage>, IRecipient<DesignModeChangedMessage>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ScoreWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public ScoreWindowViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            IsActive = true;
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

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

        public Game CurrentGame => SharedDataService.CurrentGame;
    }
}
