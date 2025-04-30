using System.Diagnostics;
using System.Security.Permissions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel : ObservableObject
    {
        public TeamInfoPageViewModel()
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
            (SharedDataService.CurrentGame.SurPlayerArray[parameter.Index].Member,
                SharedDataService.CurrentGame.SurPlayerArray[parameter.ButtonContent].Member) = 
                (SharedDataService.CurrentGame.SurPlayerArray[parameter.ButtonContent].Member,
                SharedDataService.CurrentGame.SurPlayerArray[parameter.Index].Member);

            OnPropertyChanged();
        }
    }
}
