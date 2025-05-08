using System.Diagnostics;
using System.Security.Permissions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public TeamInfoPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }
        private readonly IFilePickerService _filePickerService;
        private readonly IMessageBoxService _messageBoxService;

        public TeamInfoPageViewModel(
            ISharedDataService sharedDataService,
            IFilePickerService filePickerService,
            IMessageBoxService messageBoxService
        )
        {
            SharedDataService = sharedDataService;
            _filePickerService = filePickerService;
            _messageBoxService = messageBoxService;
            MainTeamInfoViewModel = new(
                SharedDataService.MainTeam,
                _filePickerService,
                _messageBoxService
            );

            AwayTeamInfoViewModel = new(
                SharedDataService.AwayTeam,
                _filePickerService,
                _messageBoxService
            );
        }

        public TeamInfoViewModel MainTeamInfoViewModel { get; }

        public TeamInfoViewModel AwayTeamInfoViewModel { get; }

        [RelayCommand]
        private void SwapMembersInPlayers(CharacterChangerCommandParameter parameter)
        {
            (SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Member,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Member) = 
                (SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Member,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Member);

            OnPropertyChanged();
        }
    }
}
