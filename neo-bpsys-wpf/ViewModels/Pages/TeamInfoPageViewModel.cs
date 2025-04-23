using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel : ObservableObject
    {
        public TeamInfoPageViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;
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
            _sharedDataService = sharedDataService;
            _filePickerService = filePickerService;
            _messageBoxService = messageBoxService;
            _mapper = mapper;
            MainTeamInfoViewModel = new(
                _sharedDataService.MainTeam,
                _filePickerService,
                _messageBoxService,
                _mapper
            );

            AwayTeamInfoViewModel = new(
                _sharedDataService.AwayTeam,
                _filePickerService,
                _messageBoxService,
                _mapper
            );
        }

        public TeamInfoViewModel MainTeamInfoViewModel { get; set; }

        public TeamInfoViewModel AwayTeamInfoViewModel { get; set; }
    }
}
