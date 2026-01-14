using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Collections.ObjectModel;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TeamInfoPageViewModel : ViewModelBase
{
#pragma warning disable CS8618 
    public TeamInfoPageViewModel()
#pragma warning restore CS8618 
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    public TeamInfoPageViewModel(ISharedDataService sharedDataService, IFilePickerService filePickerService)
    {
        var sharedDataService1 = sharedDataService;
        MainTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.MainTeam, filePickerService);
        AwayTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.AwayTeam, filePickerService);
        OnFieldSurPlayerViewModels =
            [.. Enumerable.Range(0, 4).Select(i => new OnFieldSurPlayerViewModel(sharedDataService1, i))];
        OnFieldHunPlayerVm = new OnFieldHunPlayerViewModel(sharedDataService1);
    }

    public TeamInfoViewModel MainTeamInfoViewModel { get; }

    public TeamInfoViewModel AwayTeamInfoViewModel { get; }

    public ObservableCollection<OnFieldSurPlayerViewModel> OnFieldSurPlayerViewModels { get; }
    public OnFieldHunPlayerViewModel OnFieldHunPlayerVm { get; }

    public partial class OnFieldSurPlayerViewModel : ObservableObjectBase
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

    public class OnFieldHunPlayerViewModel : ObservableObjectBase
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