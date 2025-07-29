using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using static neo_bpsys_wpf.Core.Enums.GameAction;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class MapBpPageViewModel : ViewModelBase, IRecipient<HighlightMessage>
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IMessageBoxService _messageBoxService;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public MapBpPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    public MapBpPageViewModel(ISharedDataService sharedDataService, IMessageBoxService messageBoxService)
    {
        _sharedDataService = sharedDataService;
        _messageBoxService = messageBoxService;
        MapSelectTeamsList =
        [
            new MapSelectTeam(_sharedDataService.MainTeam, TeamType.MainTeam),
            new MapSelectTeam(_sharedDataService.AwayTeam, TeamType.AwayTeam)
        ];
        PickMapTeam = MapSelectTeamsList[0];
        BanMapTeam = MapSelectTeamsList[0];
        BannedMap = [.. PickedMapSelections.Select(selection => new BanMapInfo(selection.Map ?? Map.无禁用))];
        IsActive = true;
    }

    private bool _breathing;

    public bool IsBreathing
    {
        get => _breathing;
        set => SetPropertyWithAction(ref _breathing, value, (oldValue) =>
        {
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this,
                nameof(IsBreathing),
                oldValue,
                value));
        });
    }

    private bool _isCampVisible;

    public bool IsCampVisible
    {
        get => _isCampVisible;
        set => SetPropertyWithAction(ref _isCampVisible, value, (oldValue) =>
        {
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this,
                nameof(IsCampVisible),
                oldValue,
                value));
        });
    }

    public int SelectedIndex => PickedMapSelections.IndexOf(PickedMapSelections.First(x => x.Map == PickedMap));

    private Map? _pickedMap;

    public Map? PickedMap
    {
        get => _pickedMap;
        set => SetPropertyWithAction(ref _pickedMap, value, (oldValue) =>
        {
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<Map?>(this,
                nameof(PickedMap),
                oldValue,
                value));
            _sharedDataService.CurrentGame.PickedMap = _pickedMap;
            OnPropertyChanged(nameof(SelectedIndex));
        });
    }

    [RelayCommand]
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
        _sharedDataService.CurrentGame.BanMap(BannedMap, BanMapTeam.Team);
        WeakReferenceMessenger.Default.Send(
            new PropertyChangedMessage<List<BanMapInfo>>(this, nameof(BannedMap), [], BannedMap));
        if (map == null) return;
        if (BannedMap.First(x => x.Map == map).IsBanned)
        {
            _sharedDataService.CurrentGame.BannedMap = map;
            _bannedMapSequence.Add((Map)map);
        }
        else
        {
            _bannedMapSequence.RemoveAt(_bannedMapSequence.Count - 1);
            if (_bannedMapSequence.Count > 0)
                _sharedDataService.CurrentGame.BannedMap = _bannedMapSequence.Last();
            else
                _sharedDataService.CurrentGame.BannedMap = null;
        }
    }

    private readonly List<Map> _bannedMapSequence = [];

    [RelayCommand]
    private async Task ResetMapBpAsync()
    {
        if (!await _messageBoxService.ShowConfirmAsync("确认提示", "是否要重置地图BP")) return;
        _sharedDataService.CurrentGame.ResetMapBp();
        PickedMap = null;
        foreach (var map in BannedMap)
        {
            map.IsBanned = false;
        }
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
                    x.TeamType == (message.Index?[0] == 0 ? TeamType.MainTeam : TeamType.AwayTeam));
                IsBreathing = true;
                break;
            case GameAction.BanMap:
                BanMapTeam = MapSelectTeamsList.First(x =>
                    x.TeamType == (message.Index?[0] == 0 ? TeamType.MainTeam : TeamType.AwayTeam));
                IsBreathing = true;
                break;
            case PickCamp:
                IsCampVisible = true;
                IsBreathing = true;
                break;
            default:
                if (!IsBreathing) IsBreathing = false;
                break;
        }
    }

    public ObservableCollection<MapSelection> PickedMapSelections { get; } =
    [
        new(),
        new(Map.军工厂, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.军工厂))),
        new(Map.红教堂, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.红教堂))),
        new(Map.圣心医院, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.圣心医院))),
        new(Map.里奥的回忆, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.里奥的回忆))),
        new(Map.月亮河公园, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.月亮河公园))),
        new(Map.湖景村, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.湖景村))),
        new(Map.永眠镇, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.永眠镇))),
        new(Map.唐人街, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.唐人街))),
        new(Map.不归林, ImageHelper.GetImageSourceFromName(ImageSourceKey.map, nameof(Map.不归林))),
    ];

    public List<MapSelectTeam> MapSelectTeamsList { get; }

    public class MapSelectTeam(Team team, TeamType teamType)
    {
        public Team Team { get; } = team;
        public TeamType TeamType { get; } = teamType;
        public string DisplayedTeamType { get; } = teamType == TeamType.MainTeam ? "主队" : "客队";
    }

    public partial class MapSelection(Map? map = null, ImageSource? imageSource = null)
        : ViewModelBase, IRecipient<PropertyChangedMessage<List<BanMapInfo>>>
    {
        public Map? Map { get; set; } = map;
        public ImageSource? ImageSource { get; set; } = imageSource;

        [ObservableProperty] private bool _canPickInvoke = true;

        public void Receive(PropertyChangedMessage<List<BanMapInfo>> message)
        {
            if (Map == null) return;
            switch (message.PropertyName)
            {
                case nameof(BannedMap):
                    CanPickInvoke = !message.NewValue.Any(x => x.Map == Map && x.IsBanned);
                    break;
            }
        }
    }
}