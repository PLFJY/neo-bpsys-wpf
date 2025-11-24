using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Controls;
using System.Collections.ObjectModel;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Messages;
using Player = neo_bpsys_wpf.Core.Models.Player;
using neo_bpsys_wpf.Core.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TeamInfoPageViewModel : ViewModelBase
{
    public TeamInfoPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    public TeamInfoPageViewModel(ISharedDataService sharedDataService, IFilePickerService filePickerService,
        IMessageBoxService messageBoxService, IASGService asgService)
    {
        var sharedDataService1 = sharedDataService;
        MainTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.MainTeam, filePickerService, messageBoxService);
        AwayTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.AwayTeam, filePickerService, messageBoxService);
        OnFieldSurPlayerViewModels =
            [.. Enumerable.Range(0, 4).Select(i => new OnFieldSurPlayerViewModel(sharedDataService1, i))];
        OnFieldHunPlayerVm = new OnFieldHunPlayerViewModel(sharedDataService1);

        _sharedDataService = sharedDataService1;
        _asgService = asgService;
    }

    public TeamInfoViewModel MainTeamInfoViewModel { get; }

    public TeamInfoViewModel AwayTeamInfoViewModel { get; }

    public ObservableCollection<OnFieldSurPlayerViewModel> OnFieldSurPlayerViewModels { get; }
    public OnFieldHunPlayerViewModel OnFieldHunPlayerVm { get; }

    private readonly ISharedDataService _sharedDataService;
    private readonly IASGService _asgService;

    

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _eventQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AsgEventDto> _eventResults = [];

    [ObservableProperty]
    private AsgEventDto? _selectedEvent;

    [ObservableProperty]
    private ObservableCollection<AsgMatchDto> _matchResults = [];

    [ObservableProperty]
    private AsgMatchDto? _selectedMatch;

    [RelayCommand]
    private async Task LoginAsync()
    {
        var ok = await _asgService.LoginAsync(Email, Password);
        IsLoggedIn = ok && _asgService.IsLoggedIn;
    }

    [RelayCommand]
    private async Task SearchEventsAsync()
    {
        var res = await _asgService.SearchEventsAsync(EventQuery);
        EventResults = new ObservableCollection<AsgEventDto>(res?.Items ?? Array.Empty<AsgEventDto>());
    }

    [RelayCommand]
    private async Task LoadMatchesAsync()
    {
        if (SelectedEvent == null) return;
        if (!Guid.TryParse(SelectedEvent.Id, out var eventId)) return;
        var list = await _asgService.GetMatchesByEventAsync(eventId);
        MatchResults = new ObservableCollection<AsgMatchDto>(list ?? Array.Empty<AsgMatchDto>());
    }

    [RelayCommand]
    private async Task ImportSelectedMatchTeamsAsync()
    {
        if (SelectedMatch == null) return;
        if (!Guid.TryParse(SelectedMatch.HomeTeamId, out var homeId)) return;
        if (!Guid.TryParse(SelectedMatch.AwayTeamId, out var awayId)) return;
        var home = await _asgService.GetTeamAsync(homeId);
        var away = await _asgService.GetTeamAsync(awayId);
        if (home == null || away == null) return;
        var mainTeam = ConvertFromAsgTeam(home, _sharedDataService.MainTeam.Camp);
        var awayTeam = ConvertFromAsgTeam(away, _sharedDataService.AwayTeam.Camp);
        _sharedDataService.MainTeam.ImportTeamInfo(mainTeam);
        _sharedDataService.AwayTeam.ImportTeamInfo(awayTeam);
        MainTeamInfoViewModel.TeamName = _sharedDataService.MainTeam.Name;
        AwayTeamInfoViewModel.TeamName = _sharedDataService.AwayTeam.Name;
    }

    private static Core.Models.Team ConvertFromAsgTeam(AsgTeamDto t, Core.Enums.Camp camp)
    {
        var surList = new ObservableCollection<Core.Models.Member>(Enumerable.Range(0, 4).Select(_ => new Core.Models.Member(Core.Enums.Camp.Sur)));
        var hunList = new ObservableCollection<Core.Models.Member>(new[] { new Core.Models.Member(Core.Enums.Camp.Hun) });
        var players = t.Players ?? Array.Empty<AsgPlayerDto>();
        var names = players.Select(p => p.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        for (var i = 0; i < Math.Min(4, names.Count); i++)
        {
            surList[i].Name = names[i];
        }
        if (names.Count > 0)
        {
            hunList[0].Name = names[0];
        }
        var team = new Core.Models.Team(t.Name ?? string.Empty, t.LogoUrl ?? string.Empty, surList, hunList);
        return team;
    }

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
