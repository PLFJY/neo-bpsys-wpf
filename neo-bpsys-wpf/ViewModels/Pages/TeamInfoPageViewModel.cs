using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using System.Collections.ObjectModel;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TeamInfoPageViewModel : ViewModelBase
{
    public TeamInfoPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    public TeamInfoPageViewModel(ISharedDataService sharedDataService, IFilePickerService filePickerService,
        IMessageBoxService messageBoxService)
    {
        var sharedDataService1 = sharedDataService;
        MainTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.MainTeam, filePickerService, messageBoxService);
        AwayTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.AwayTeam, filePickerService, messageBoxService);
        OnFieldSurPlayerViewModels =
            [.. Enumerable.Range(0, 4).Select(i => new OnFieldSurPlayerViewModel(sharedDataService1, i))];
        OnFieldHunPlayerVm = new OnFieldHunPlayerViewModel(sharedDataService1);
    }

    public TeamInfoViewModel MainTeamInfoViewModel { get; }

    public TeamInfoViewModel AwayTeamInfoViewModel { get; }

    public ObservableCollection<OnFieldSurPlayerViewModel> OnFieldSurPlayerViewModels { get; }
    public OnFieldHunPlayerViewModel OnFieldHunPlayerVm { get; }

    public partial class OnFieldSurPlayerViewModel : ViewModelBase
    {
        private readonly ISharedDataService _sharedDataService;

        public OnFieldSurPlayerViewModel(ISharedDataService sharedDataService, int index)
        {
            _sharedDataService = sharedDataService;
            Index = index;
            sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
        }

        public Player ThisPlayer => _sharedDataService.CurrentGame.SurPlayerList[Index];

        public int Index { get; }

        [RelayCommand]
        private void SwapMembersInPlayers(CharacterChangerCommandParameter parameter)
        {
            _sharedDataService.CurrentGame.SwapMembersInPlayers(parameter.Source, parameter.Target);
        }
    }

    public class OnFieldHunPlayerViewModel : ViewModelBase
    {
        private readonly ISharedDataService _sharedDataService;

        public OnFieldHunPlayerViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
        }

        public Player ThisPlayer => _sharedDataService.CurrentGame.HunPlayer;
    }
}