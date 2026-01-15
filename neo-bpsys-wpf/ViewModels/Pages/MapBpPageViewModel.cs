using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class MapBpPageViewModel : ViewModelBase, IRecipient<HighlightMessage>
{
    private readonly ISharedDataService _sharedDataService;


#pragma warning disable CS8618 
    public MapBpPageViewModel()
#pragma warning restore CS8618 

    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    public MapBpPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        MapSelectTeamsList =
        [
            new MapSelectTeam(_sharedDataService.HomeTeam, TeamType.HomeTeam),
            new MapSelectTeam(_sharedDataService.AwayTeam, TeamType.AwayTeam)
        ];
        PickMapTeam = MapSelectTeamsList[0];
        BanMapTeam = MapSelectTeamsList[1];
        PickedMapSelections.Add(new MapSelection());
        foreach (var mapV2 in sharedDataService.CurrentGame.MapV2Dictionary.Values.Where(x => x.MapName != Map.NoBans))
        {
            PickedMapSelections.Add(new MapSelection(mapV2));
        }

        BannedMap = [.. sharedDataService.CurrentGame.MapV2Dictionary.Values.Select(mapV2 => new BanMapInfo(mapV2))];
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentGame));
            sharedDataService.CurrentGame.PropertyChanged += OnCurrentGameSelectedMapChanged;
            _pickedMap = CurrentGame.PickedMap;
            OnPropertyChanged(nameof(PickedMap));
        };
        sharedDataService.IsMapV2BreathingChanged += (_, _) => IsBreathing = sharedDataService.IsMapV2Breathing;
        sharedDataService.IsMapV2CampVisibleChanged += (_, _) => IsCampVisible = sharedDataService.IsMapV2CampVisible;
        sharedDataService.CurrentGame.PropertyChanged += OnCurrentGameSelectedMapChanged;
    }

    private void OnCurrentGameSelectedMapChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Game.PickedMap))
        {
            _pickedMap = CurrentGame.PickedMap;
            OnPropertyChanged(nameof(PickedMap));
        }
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    private bool _breathing;

    public bool IsBreathing
    {
        get => _breathing;
        set => SetPropertyWithAction(ref _breathing, value,
            (_) => { _sharedDataService.IsMapV2Breathing = value; });
    }

    private bool _isCampVisible;

    public bool IsCampVisible
    {
        get => _isCampVisible;
        set => SetPropertyWithAction(ref _isCampVisible, value,
            (_) => { _sharedDataService.IsMapV2CampVisible = value; });
    }

    private Map? _pickedMap;

    public Map? PickedMap
    {
        get => _pickedMap;
        set => SetPropertyWithAction(ref _pickedMap, value, (oldValue) =>
        {
            _sharedDataService.CurrentGame.PickedMap = _pickedMap;
            PickMap(_pickedMap);
        });
    }

    private void PickMap(Map? map)
    {
        _sharedDataService.CurrentGame.PickMap(map, PickMapTeam.Team);
    }

    [ObservableProperty] private MapSelectTeam _pickMapTeam;

    public List<BanMapInfo> BannedMap { get; }

    [ObservableProperty] private MapSelectTeam _banMapTeam;

    [RelayCommand]
    private void BanMap(Map? map = null)
    {
        if (map == null) return;
        if (CurrentGame.MapV2Dictionary.TryGetValue(map.ToString()!, out var mapV2) && mapV2 is { IsBanned: true })
        {
            mapV2.OperationTeam = BanMapTeam.Team;
            _sharedDataService.CurrentGame.BannedMap = map;
            _bannedMapSequence.Add((Map)map);
        }
        else if (mapV2 is { IsBanned: false })
        {
            mapV2.OperationTeam = null;
            _bannedMapSequence.Remove(map);
            _sharedDataService.CurrentGame.BannedMap = _bannedMapSequence.Count > 0 ? _bannedMapSequence.Last() : null;
        }
    }

    private readonly List<Map?> _bannedMapSequence = [];

    [RelayCommand]
    private async Task ResetMapBpAsync()
    {
        if (!await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("AreYouSureToResetMapBP"),
                I18nHelper.GetLocalizedString("Tips"),
                I18nHelper.GetLocalizedString("Confirm"),
                I18nHelper.GetLocalizedString("Cancel"))) return;
        _sharedDataService.CurrentGame.ResetMapBp();
        PickedMap = null;
    }

    [ObservableProperty] private bool _isPickHighlighted;

    [ObservableProperty] private bool _isBanHighlighted;

    public void Receive(HighlightMessage message)
    {
        IsPickHighlighted = message.GameAction == GameAction.PickMap;
        IsBanHighlighted = message.GameAction == GameAction.BanMap;
        switch (message.GameAction)
        {
            case GameAction.PickMap:
                PickMapTeam = MapSelectTeamsList.First(x =>
                    x.TeamType == (message.Index?[0] == 0 ? TeamType.HomeTeam : TeamType.AwayTeam));
                IsBreathing = true;
                IsCampVisible = false;
                break;
            case GameAction.BanMap:
                BanMapTeam = MapSelectTeamsList.First(x =>
                    x.TeamType == (message.Index?[0] == 0 ? TeamType.HomeTeam : TeamType.AwayTeam));
                IsBreathing = true;
                IsCampVisible = false;
                break;
            case GameAction.PickCamp:
                IsCampVisible = true;
                IsBreathing = true;
                break;
            default:
                if (IsBreathing) IsBreathing = false;
                break;
        }
    }

    public ObservableCollection<MapSelection> PickedMapSelections { get; } = [];

    public List<MapSelectTeam> MapSelectTeamsList { get; }

    public class MapSelectTeam(Team team, TeamType teamType)
    {
        public Team Team { get; } = team;
        public TeamType TeamType { get; } = teamType;
    }

    public class MapSelection(MapV2? map = null)
    {
        public MapV2 Map { get; } = map ?? new MapV2(null);

        public ImageSource? ImageSource { get; } =
            ImageHelper.GetImageSourceFromName(ImageSourceKey.map, map?.MapName.ToString());
    }

    public class BanMapInfo(MapV2 map)
    {
        public MapV2 Map { get; } = map;

        public ImageSource? ImageSource { get; } =
            ImageHelper.GetImageSourceFromName(ImageSourceKey.map_singleColor, map.MapName.ToString());
    }
}