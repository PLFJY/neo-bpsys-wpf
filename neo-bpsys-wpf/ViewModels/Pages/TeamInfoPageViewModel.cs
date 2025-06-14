using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Reflection;
using static neo_bpsys_wpf.ViewModels.Pages.BanHunPageViewModel;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel : ObservableRecipient
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public TeamInfoPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;
        private readonly IFilePickerService _filePickerService;
        private readonly IMessageBoxService _messageBoxService;

        public TeamInfoPageViewModel(ISharedDataService sharedDataService, IFilePickerService filePickerService, IMessageBoxService messageBoxService)
        {
            _sharedDataService = sharedDataService;
            _filePickerService = filePickerService;
            _messageBoxService = messageBoxService;
            MainTeamInfoViewModel = new(_sharedDataService.MainTeam, _filePickerService, _messageBoxService);
            AwayTeamInfoViewModel = new(_sharedDataService.AwayTeam, _filePickerService, _messageBoxService);
            OnFieldSurPlayerViewModels = [.. Enumerable.Range(0, 4).Select(i => new OnFieldSurPlayerViewModel(_sharedDataService, i))];
            OnFieldHunPlayerVm = new(_sharedDataService);
        }

        public TeamInfoViewModel MainTeamInfoViewModel { get; }

        public TeamInfoViewModel AwayTeamInfoViewModel { get; }

        public ObservableCollection<OnFieldSurPlayerViewModel> OnFieldSurPlayerViewModels { get; set; }
        public OnFieldHunPlayerViewModel OnFieldHunPlayerVm { get; set; }

        public partial class OnFieldSurPlayerViewModel :
            ObservableRecipient, IRecipient<MemberStateChangedMessage>, IRecipient<PlayerSwappedMessage>, IRecipient<SwapMessage>
        {
            private readonly ISharedDataService _sharedDataService;

            public OnFieldSurPlayerViewModel(ISharedDataService sharedDataService, int index)
            {
                _sharedDataService = sharedDataService;
                Index = index;
                IsActive = true;
            }

            public Player ThisPlayer => _sharedDataService.CurrentGame.SurPlayerList[Index];

            public int Index { get; }

            public void Receive(MemberStateChangedMessage message)
            {
                OnPropertyChanged(nameof(ThisPlayer));
            }

            public void Receive(PlayerSwappedMessage message)
            {
                OnPropertyChanged(nameof(ThisPlayer));
            }

            public void Receive(SwapMessage message)
            {
                OnPropertyChanged(nameof(ThisPlayer));
            }

            [RelayCommand]
            private void SwapMembersInPlayers(CharacterChangerCommandParameter parameter)
            {
                (_sharedDataService.CurrentGame.SurPlayerList[parameter.Target].Member,
                    _sharedDataService.CurrentGame.SurPlayerList[parameter.Source].Member) =
                    (_sharedDataService.CurrentGame.SurPlayerList[parameter.Source].Member,
                    _sharedDataService.CurrentGame.SurPlayerList[parameter.Target].Member);
                WeakReferenceMessenger.Default.Send(new PlayerSwappedMessage(this));

                OnPropertyChanged();
            }
        }

        public class OnFieldHunPlayerViewModel : 
            ObservableRecipient, IRecipient<MemberStateChangedMessage>, IRecipient<SwapMessage>
        {
            private readonly ISharedDataService _sharedDataService;

            public OnFieldHunPlayerViewModel(ISharedDataService sharedDataService)
            {
                _sharedDataService = sharedDataService;
                IsActive = true;
            }

            public Player ThisPlayer => _sharedDataService.CurrentGame.HunPlayer;

            public void Receive(MemberStateChangedMessage message)
            {
                OnPropertyChanged(nameof(ThisPlayer));
            }

            public void Receive(SwapMessage message)
            {
                OnPropertyChanged(nameof(ThisPlayer));
            }
        }

    }
}
