using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 对局类, 创建需要导入 <see cref="Core.Models.Game.SurTeam"/> 和 <see cref="Core.Models.Game.HunTeam"/> 两支队伍以及对局进度
/// </summary>
public partial class Game : ObservableObjectBase
{
    #region 对局基本信息

    /// <summary>
    /// 对局GUID
    /// </summary>
    public Guid Guid { get; }

    /// <summary>
    /// 对局开始时间
    /// </summary>
    public DateTime StartTime { get; }

    private Team _surTeam = new(Camp.Sur, TeamType.HomeTeam);

    /// <summary>
    /// 求生者队伍
    /// </summary>
    public Team SurTeam
    {
        get => _surTeam;
        private set
        {
            SurTeam.MemberOnFieldChanged -= OnMemberOnFieldChanged;
            SetProperty(ref _surTeam, value);
            SurTeam.MemberOnFieldChanged += OnMemberOnFieldChanged;
        }
    }

    private Team _hunTeam = new(Camp.Hun, TeamType.AwayTeam);

    /// <summary>
    /// 监管者队伍
    /// </summary>
    public Team HunTeam
    {
        get => _hunTeam;
        private set
        {
            HunTeam.MemberOnFieldChanged -= OnMemberOnFieldChanged;
            SetProperty(ref _hunTeam, value);
            HunTeam.MemberOnFieldChanged += OnMemberOnFieldChanged;
        }
    }

    /// <summary>
    /// 对局进度
    /// </summary>
    [ObservableProperty] private GameProgress _gameProgress;

    #endregion

    #region 角色禁用

    /// <summary>
    /// 当局求生者禁用列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<Character?> _currentSurBannedList = [];

    /// <summary>
    /// 当局监管者禁用列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<Character?> _currentHunBannedList = [];

    #endregion

    /// <summary>
    /// Game的构造函数
    /// </summary>
    /// <param name="surTeam">求生者队伍</param>
    /// <param name="hunTeam">监管者队伍</param>
    /// <param name="gameProgress">对局进度</param>
    /// <param name="pickedMap">选择的地图</param>
    /// <param name="bannedMap">Ban掉的地图(V1)</param>
    /// <param name="mapV2Dictionary">地图V2字典</param>
    /// <param name="guid">对局GUID(用于恢复记录)</param>
    /// <param name="startTime">对局开始时间(用于恢复记录)</param>
    /// <param name="surPlayersData">求生者选手数据(用于恢复记录)</param>
    /// <param name="hunPlayerData">监管者选手数据(用于恢复记录)</param>
    /// <param name="currentSurBannedList">求生者禁用列表(用于恢复记录)</param>
    /// <param name="currentHunBannedList">监管者禁用列表(用于恢复记录)</param>
    [JsonConstructor]
    public Game(Team surTeam, Team hunTeam,
        GameProgress gameProgress, Map? pickedMap = null, Map? bannedMap = null,
        Dictionary<string, MapV2>? mapV2Dictionary = null,
        Guid guid = default, DateTime startTime = default,
        ObservableCollection<Player>? surPlayersData = null,
        Player? hunPlayerData = null,
        ObservableCollection<Character?>? currentSurBannedList = null,
        ObservableCollection<Character?>? currentHunBannedList = null)
    {
        //基本信息初始化
        Guid = guid == Guid.Empty ? Guid.NewGuid() : guid;
        StartTime = startTime == default ? DateTime.Now : startTime;
        //初始化队伍信息
        SurTeam = surTeam;
        SurTeam.Camp = Camp.Sur;
        HunTeam = hunTeam;
        HunTeam.Camp = Camp.Hun;
        //初始化对局进度
        GameProgress = gameProgress;

        //创建Player
        SurPlayersData =
        [
            .. Enumerable.Range(0, 4).Select(i =>
                new Player(SurTeam.SurMemberOnFieldCollection[i] ?? new Member(Camp.Sur)))
        ];

        SurPlayerList = new ReadOnlyObservableCollection<Player>(SurPlayersData);
        HunPlayer = new Player(HunTeam.HunMemberOnField ?? new Member(Camp.Hun));
        //装填Members
        LoadMembers();

        //恢复Player数据
        if (surPlayersData != null)
        {
            for (var i = 0; i < 4 && i < surPlayersData.Count; i++)
            {
                var savedData = surPlayersData[i];
                SurPlayerList[i].Character = savedData.Character;
                SurPlayerList[i].Talent = savedData.Talent;
                SurPlayerList[i].Trait = savedData.Trait;
                SurPlayerList[i].Data = savedData.Data;
            }
        }

        if (hunPlayerData != null)
        {
            HunPlayer.Character = hunPlayerData.Character;
            HunPlayer.Talent = hunPlayerData.Talent;
            HunPlayer.Trait = hunPlayerData.Trait;
            HunPlayer.Data = hunPlayerData.Data;
        }

        //新建角色禁用列表
        CurrentSurBannedList = currentSurBannedList ??
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select(_ => new Character(Camp.Sur))
        ];
        CurrentHunBannedList = currentHunBannedList ??
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(_ => new Character(Camp.Hun))
        ];

        //填充全局禁选列表
        SurTeam.UpdateGlobalBanFromRecord();
        HunTeam.UpdateGlobalBanFromRecord();

        //初始化地图信息
        if (pickedMap != null) PickedMap = pickedMap;
        if (bannedMap != null) BannedMap = bannedMap;
        MapV2Dictionary = mapV2Dictionary ?? [];
        if (MapV2Dictionary.Count != 0) return;
        //如果地图V2的字典为空则添加地图信息
        foreach (var map in Enum.GetValues<Map>())
        {
            MapV2Dictionary.Add(map.ToString(), new MapV2(map));
        }
    }

    #region 上场选手

    [JsonInclude] internal ObservableCollection<Player> SurPlayersData { get; }

    /// <summary>
    /// 上场选手(求生者)
    /// </summary>
    [JsonIgnore] public ReadOnlyObservableCollection<Player> SurPlayerList { get; }

    [JsonInclude] internal Player HunPlayerData => HunPlayer;

    /// <summary>
    /// 上场选手(监管者)
    /// </summary>
    [JsonIgnore] public Player HunPlayer { get; }

    /// <summary>
    /// 装填Members
    /// </summary>
    private void LoadMembers()
    {
        //求生者
        for (var i = 0; i < SurPlayerList.Count; i++)
        {
            SurPlayerList[i].Member = SurTeam.SurMemberOnFieldCollection[i] ?? new Member(Camp.Sur);
        }

        //监管者
        HunPlayer.Member = HunTeam.HunMemberOnField ?? new Member(Camp.Hun);
    }

    private void OnMemberOnFieldChanged(object? sender, EventArgs args) => LoadMembers();

    #endregion

    #region 地图BP

    private Map? _pickedMap;

    /// <summary>
    /// 选择的地图
    /// </summary>
    public Map? PickedMap
    {
        get => _pickedMap;
        set => SetPropertyWithAction(ref _pickedMap, value,
            _ =>
            {
                PickedMapImage = ImageHelper.GetImageSourceFromName(ImageSourceKey.map, _pickedMap.ToString());
                PickedMapImageLarge =
                    ImageHelper.GetImageSourceFromName(ImageSourceKey.map_square, _pickedMap.ToString());
            });
    }

    private Map? _bannedMap;

    /// <summary>
    /// ban掉的地图 (V1)
    /// </summary>
    public Map? BannedMap
    {
        get => _bannedMap;
        set => SetPropertyWithAction(ref _bannedMap, value,
            _ => BannedMapImage =
                ImageHelper.GetImageSourceFromName(ImageSourceKey.map_singleColor, _bannedMap.ToString()));
    }

    /// <summary>
    /// 选择的地图的图片
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _pickedMapImage;

    /// <summary>
    /// 选择的地图的图片
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _pickedMapImageLarge;

    /// <summary>
    /// Ban掉的地图的图片
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _bannedMapImage;

    /// <summary>
    /// 地图V2字典
    /// </summary>
    public Dictionary<string, MapV2> MapV2Dictionary { get; }

    /// <summary>
    /// 选择地图
    /// </summary>
    /// <param name="map">地图</param>
    /// <param name="team">操作队伍</param>
    /// <exception cref="InvalidOperationException">地图不存在</exception>
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

        var matchedMap = MapV2Dictionary.Values.First(x => x.MapName == map);
        matchedMap.OperationTeam = team;
        matchedMap.IsPicked = true;
        matchedMap.IsBanned = false;

        foreach (var mapValue in MapV2Dictionary.Values.Where(x => x.IsPicked && x != matchedMap))
        {
            mapValue.IsPicked = false;
            mapValue.OperationTeam = null;
        }
    }

    /// <summary>
    /// 重置地图BP
    /// </summary>
    public void ResetMapBp()
    {
        PickedMap = null;
        BannedMap = null;
        foreach (var map in MapV2Dictionary)
        {
            map.Value.IsPicked = false;
            map.Value.OperationTeam = null;
            map.Value.IsBanned = false;
        }
    }

    #endregion

    #region 换边

    /// <summary>
    /// 换边
    /// </summary>
    public void Swap()
    {
        (SurTeam.Camp, HunTeam.Camp) = (HunTeam.Camp, SurTeam.Camp);
        (SurTeam, HunTeam) = (HunTeam, SurTeam);
        TeamSwapped?.Invoke(this, EventArgs.Empty);
        LoadMembers();
    }

    /// <summary>
    /// 换边事件
    /// </summary>
    public event EventHandler? TeamSwapped;

    #endregion

    #region 选手/角色换位

    /// <summary>
    /// 选手换位
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="target">目标</param>
    public void SwapMembersInPlayers(int source, int target)
    {
        (SurPlayersData[source].Member, SurPlayersData[target].Member) =
            (SurPlayersData[target].Member, SurPlayersData[source].Member);
    }

    /// <summary>
    /// 角色换位
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="target">目标</param>
    public void SwapCharactersInPlayers(int source, int target)
    {
        (SurPlayersData[source].Character, SurPlayersData[target].Character) =
            (SurPlayersData[target].Character, SurPlayersData[source].Character);
    }

    #endregion
}