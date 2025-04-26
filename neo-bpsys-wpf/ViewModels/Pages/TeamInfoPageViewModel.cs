using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Diagnostics;
using System.Security.Permissions;

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
        private readonly IMapper _mapper;

        public TeamInfoPageViewModel(
            ISharedDataService sharedDataService,
            IFilePickerService filePickerService,
            IMessageBoxService messageBoxService,
            IMapper mapper
        )
        {
            SharedDataService = sharedDataService;
            _filePickerService = filePickerService;
            _messageBoxService = messageBoxService;
            _mapper = mapper;
            MainTeamInfoViewModel = new(
                SharedDataService.MainTeam,
                _filePickerService,
                _messageBoxService,
                _mapper
            );

            AwayTeamInfoViewModel = new(
                SharedDataService.AwayTeam,
                _filePickerService,
                _messageBoxService,
                _mapper
            );
        }

        public TeamInfoViewModel MainTeamInfoViewModel { get; }

        public TeamInfoViewModel AwayTeamInfoViewModel { get; }

        [RelayCommand]
        private void SwapMembersInPlayers(object sender)
        {
            if(sender is string buttonName)
                Debug.WriteLine(buttonName);
        }

        public List<Player> NowPlayers { get; }
    }
}
