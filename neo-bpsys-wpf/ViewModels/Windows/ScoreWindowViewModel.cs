using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.ComponentModel.Design.Serialization;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class ScoreWindowViewModel : 
        ObservableRecipient, 
        IRecipient<NewGameMessage>, 
        IRecipient<DesignModeChangedMessage>,
        IRecipient<PropertyChangedMessage<int>>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ScoreWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;

        public ScoreWindowViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            IsActive = true;
        }

        [ObservableProperty]
        private bool _isDesignMode = false;

        [ObservableProperty]
        private int _totalMainMinorPoint = 0;

        [ObservableProperty]
        private int _totalAwayMinorPoint = 0;

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
            if(message.PropertyName == nameof(TotalMainMinorPoint))
            {
                TotalMainMinorPoint = message.NewValue;
            }
            if(message.PropertyName == nameof(TotalAwayMinorPoint))
            {
                TotalAwayMinorPoint = message.NewValue;
            }
        }

        public Game CurrentGame => _sharedDataService.CurrentGame;

        public Team MainTeam => _sharedDataService.MainTeam;
        public Team AwayTeam => _sharedDataService.AwayTeam;
    }
}
