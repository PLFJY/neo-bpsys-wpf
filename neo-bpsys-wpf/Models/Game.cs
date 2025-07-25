using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using System.Linq;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 对局类, 创建需要导入 <see cref="SurTeam"/> 和 <see cref="HunTeam"/> 两支队伍以及对局进度
/// </summary>
public partial class Game : ObservableRecipient, IRecipient<MemberOnFieldChangedMessage>, IRecipient<NewGameMessage>
{
    public Guid Guid { get; }

    public DateTime StartTime { get; }

    [ObservableProperty] private Team _surTeam = new(Camp.Sur);

    [ObservableProperty] private Team _hunTeam = new(Camp.Hun);

    [ObservableProperty] private GameProgress _gameProgress;

    private Map? _pickedMap;

    public Map? PickedMap
    {
        get => _pickedMap;
        set
        {
            _pickedMap = value;
            PickedMapImage = ImageHelper.GetImageSourceFromName(ImageSourceKey.map, _pickedMap.ToString());
            OnPropertyChanged();
        }
    }


    private Map? _bannedMap;

    public Map? BannedMap
    {
        get => _bannedMap;
        set
        {
            _bannedMap = value;
            BannedMapImage = ImageHelper.GetImageSourceFromName(ImageSourceKey.map_singleColor, _bannedMap.ToString());
            OnPropertyChanged();
        }
    }

    [ObservableProperty] [property: JsonIgnore]
    private ImageSource? _pickedMapImage;

    [ObservableProperty] [property: JsonIgnore]
    private ImageSource? _bannedMapImage;

    public Dictionary<string, MapV2> MapV2Dictionary { get; } = [];

    [ObservableProperty] private ObservableCollection<Character?> _currentHunBannedList = [];

    [ObservableProperty] private ObservableCollection<Character?> _currentSurBannedList = [];

    [ObservableProperty] private ObservableCollection<Player> _surPlayerList;

    [ObservableProperty] private Player _hunPlayer;

    public Game(Team surTeam, Team hunTeam, GameProgress gameProgress)
    {
        IsActive = true;
        Guid = Guid.NewGuid();
        StartTime = DateTime.Now;
        SurTeam = surTeam;
        HunTeam = hunTeam;
        //创建Player
        SurPlayerList =
        [
            .. Enumerable.Range(0, 4).Select(i => new Player(SurTeam.SurMemberOnFieldList[i] ?? new Member(Camp.Sur),
                SurTeam.SurMemberOnFieldList[i] != null))
        ];
        HunPlayer = new Player(HunTeam.HunMemberOnField ?? new Member(Camp.Hun), HunTeam.HunMemberOnField != null);
        LoadMembers();

        GameProgress = gameProgress;
        //新建角色禁用列表
        CurrentHunBannedList = [.. Enumerable.Range(0, 2).Select(i => new Character(Camp.Hun))];
        CurrentSurBannedList = [.. Enumerable.Range(0, 4).Select(i => new Character(Camp.Sur))];
        OnPropertyChanged(nameof(SurTeam));
        OnPropertyChanged(nameof(HunTeam));
        OnPropertyChanged(string.Empty);

        foreach (var map in Enum.GetValues<Map>())
        {
            MapV2Dictionary.Add(map.ToString(), new(map));
        }
    }

    private void LoadMembers()
    {
        for (var i = 0; i < SurPlayerList.Count; i++)
        {
            SurPlayerList[i].Member = SurTeam.SurMemberOnFieldList[i] ?? new Member(Camp.Sur);
        }

        HunPlayer.Member = HunTeam.HunMemberOnField ?? new Member(Camp.Hun);
    }

    public void Receive(MemberOnFieldChangedMessage message) => LoadMembers();

    public void Receive(NewGameMessage message) => LoadMembers();

    public void PickMap(Map? map, Team team)
    {
        if (map == null)
        {
            foreach (var mapV2 in MapV2Dictionary.Values.Where(mapV2 => !mapV2.IsBanned))
            {
                mapV2.IsPicked = false;
                mapV2.OperationTeam = null;
            }

            return;
        }

        var matchedMap = MapV2Dictionary.Values
                             .FirstOrDefault(x => x.MapName == map)
                         ?? throw new InvalidOperationException($"未找到地图: {map}");

        matchedMap.OperationTeam = team;
        matchedMap.IsPicked = true;
        matchedMap.IsBanned = false;

        foreach (var mapValue in MapV2Dictionary.Values.Where(x => x.IsPicked && x != matchedMap))
        {
            mapValue.IsPicked = false;
            mapValue.OperationTeam = null;
        }
    }

    public void BanMap(List<MapBpPageViewModel.BanMapInfo> mapList, Team? team = null)
    {
        foreach (var map in mapList)
        {
            var matchedMap = MapV2Dictionary.Values
                                 .FirstOrDefault(x => x.MapName == map.Map)
                             ?? throw new InvalidOperationException($"未找到符合条件的地图: {map}");
            
            if (matchedMap.IsPicked) continue;
            
            matchedMap.IsBanned = map.IsBanned;
            matchedMap.OperationTeam = map.IsBanned ? team : null;
        }
    }

    public void SwitchMapV2CampVisible(bool? state = null)
    {
        foreach (var mapValue in MapV2Dictionary.Values
                     .Where(x => x.IsPicked || x.IsBanned))
        {
            mapValue.IsCampVisible = state ?? !mapValue.IsCampVisible;
        }
    }
}