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
using neo_bpsys_wpf.Core.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 地图 BP 页面视图模型，管理地图选择和禁用流程。
/// </summary>
public partial class MapBpPageViewModel : ViewModelBase, IRecipient<HighlightMessage>
{
    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService? _settingsHostService;


    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
#pragma warning disable CS8618 
    public MapBpPageViewModel()
#pragma warning restore CS8618 

    {
        // Decorative constructor for design-time only.
    }

    /// <summary>
    /// 初始化地图 BP 页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    public MapBpPageViewModel(ISharedDataService sharedDataService)
        : this(sharedDataService, null)
    {
    }

    /// <summary>
    /// 初始化地图 BP 页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="settingsHostService">设置宿主服务</param>
    public MapBpPageViewModel(ISharedDataService sharedDataService, ISettingsHostService? settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
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

    /// <summary>
    /// 获取当前比赛数据。
    /// </summary>
    public Game CurrentGame => _sharedDataService.CurrentGame;

    private bool _breathing;

    /// <summary>
    /// 获取或设置地图是否处于呼吸灯闪烁状态。
    /// </summary>
    public bool IsBreathing
    {
        get => _breathing;
        set => SetPropertyWithAction(ref _breathing, value,
            (_) => { _sharedDataService.IsMapV2Breathing = value; });
    }

    private bool _isCampVisible;

    /// <summary>
    /// 获取或设置地图阵营是否可见。
    /// </summary>
    public bool IsCampVisible
    {
        get => _isCampVisible;
        set => SetPropertyWithAction(ref _isCampVisible, value,
            (_) => { _sharedDataService.IsMapV2CampVisible = value; });
    }

    private Map? _pickedMap;

    /// <summary>
    /// 获取或设置当前选中的地图。
    /// </summary>
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

    /// <summary>
    /// 当前选图队伍。
    /// </summary>
    [ObservableProperty]
    public partial MapSelectTeam PickMapTeam { get; set; }

    partial void OnPickMapTeamChanged(MapSelectTeam value)
    {
        if (_sharedDataService is null || _pickedMap is null) return;
        PickMap(_pickedMap);
    }

    /// <summary>
    /// 已禁用的地图列表。
    /// </summary>
    public List<BanMapInfo> BannedMap { get; }

    /// <summary>
    /// 当前禁用地图队伍。
    /// </summary>
    [ObservableProperty]
    public partial MapSelectTeam BanMapTeam { get; set; }

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

    [ObservableProperty]
    public partial bool IsPickHighlighted { get; set; }

    [ObservableProperty]
    public partial bool IsBanHighlighted { get; set; }

    public void Receive(HighlightMessage message)
    {
        IsPickHighlighted = message.GameAction == GameAction.PickMap;
        IsBanHighlighted = message.GameAction == GameAction.BanMap;
        switch (message.GameAction)
        {
            case GameAction.PickMap:
                PickMapTeam = MapSelectTeamsList.First(x =>
                    x.TeamType == (message.Index?[0] == 0 ? TeamType.HomeTeam : TeamType.AwayTeam));
                break;
            case GameAction.BanMap:
                BanMapTeam = MapSelectTeamsList.First(x =>
                    x.TeamType == (message.Index?[0] == 0 ? TeamType.HomeTeam : TeamType.AwayTeam));
                break;
        }
    }

    public ObservableCollection<MapSelection> PickedMapSelections { get; } = [];

    public List<MapSelectTeam> MapSelectTeamsList { get; }

    /// <summary>
    /// 地图选择队伍项。
    /// </summary>
    /// <param name="team">队伍数据。</param>
    /// <param name="teamType">队伍类型（主/客队）。</param>
    public class MapSelectTeam(Team team, TeamType teamType)
    {
        /// <summary>队伍数据。</summary>
        public Team Team { get; } = team;
        /// <summary>队伍类型。</summary>
        public TeamType TeamType { get; } = teamType;
    }

    /// <summary>
    /// 地图选择项，包含地图数据和对应图片。
    /// </summary>
    /// <param name="map">地图数据。</param>
    public class MapSelection(MapV2? map = null)
    {
        /// <summary>地图数据。</summary>
        public MapV2 Map { get; } = map ?? new MapV2(null);

        /// <summary>地图图片源。</summary>
        public ImageSource? ImageSource { get; } =
            ImageHelper.GetImageSourceFromName(ImageSourceKey.map, map?.MapName.ToString());
    }

    /// <summary>
    /// 禁用地图信息，包含地图数据和禁用状态图片。
    /// </summary>
    /// <param name="map">地图数据。</param>
    public class BanMapInfo(MapV2 map)
    {
        private ImageSource? _imageSource;

        /// <summary>地图数据。</summary>
        public MapV2 Map { get; } = map;

        /// <summary>禁用状态下的地图图片源。</summary>
        public ImageSource? ImageSource
        {
            get
            {
                if (_imageSource == null)
                {
                    if (Map.MapName == Core.Enums.Map.NoBans)
                    {
                        _imageSource = ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "BanMark");
                    }
                    else
                    {
                        _imageSource ??= ImageHelper.GetImageSourceFromName(ImageSourceKey.map, Map.MapName.ToString())?.ToGrayKeepAlpha();
                        var banMark = ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "BanMark");
                        if (banMark != null)
                            _imageSource = _imageSource?.Overlay(banMark);
                    }
                }
                return _imageSource;
            }
        }
    }
}
