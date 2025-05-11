using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Security.Policy;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示游戏核心数据模型，包含队伍配置、游戏进度、地图选择等核心属性
/// 实现属性变更通知机制，支持MVVM模式下的数据绑定
/// </summary>
public partial class Game : ObservableObject
{
    /// <summary>
    /// 获取或设置游戏实例的全局唯一标识符
    /// </summary>
    public Guid GUID { get; set; }

    /// <summary>
    /// 获取或设置游戏开始时间戳（格式：yyyy-MM-dd_HH-mm-ss）
    /// </summary>
    public string StartTime { get; set; }

    /// <summary>
    /// 获取或设置求生者阵营队伍对象
    /// </summary>
    [ObservableProperty]
    private Team _surTeam = new(Camp.Sur);

    /// <summary>
    /// 获取或设置监管者阵营队伍对象
    /// </summary>
    [ObservableProperty]
    private Team _hunTeam = new(Camp.Hun);

    /// <summary>
    /// 获取或设置当前比赛进度状态
    /// </summary>
    [ObservableProperty]
    private GameProgress _gameProgress;

    private Map? _pickedMap;

    /// <summary>
    /// 获取或设置已选择的地图对象
    /// 设置时同步更新对应的地图图片资源，并触发属性变更通知
    /// </summary>
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

    /// <summary>
    /// 获取或设置被禁用的地图对象
    /// 设置时同步更新对应的单色地图图片资源，并触发属性变更通知
    /// </summary>
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

    /// <summary>
    /// 获取或设置已选地图的可视化图片资源（不参与JSON序列化）
    /// </summary>
    [ObservableProperty]
    [JsonIgnore]
    private ImageSource? _pickedMapImage;

    /// <summary>
    /// 获取或设置被禁地图的可视化图片资源（不参与JSON序列化）
    /// </summary>
    [ObservableProperty]
    [JsonIgnore]
    private ImageSource? _bannedMapImage;

    /// <summary>
    /// 获取或设置当前监管者阵营禁用的角色列表（初始化容量：2个）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Character?> _currentHunBannedList = new();

    /// <summary>
    /// 获取或设置当前求生者阵营禁用的角色列表（初始化容量：4个）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Character?> _currentSurBannedList = new();

    /// <summary>
    /// 获取或设置求生者阵营玩家列表（绑定到队伍的场上玩家列表）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Player> _surPlayerList;

    /// <summary>
    /// 获取或设置监管者阵营当前玩家（绑定到队伍的当前监管者玩家）
    /// </summary>
    [ObservableProperty]
    private Player _hunPlayer;

    /// <summary>
    /// 初始化游戏实例
    /// </summary>
    /// <param name="surTeam">求生者阵营队伍对象</param>
    /// <param name="hunTeam">监管者阵营队伍对象</param>
    /// <param name="gameProgress">初始比赛进度状态</param>
    public Game(Team surTeam, Team hunTeam, GameProgress gameProgress)
    {
        GUID = Guid.NewGuid();
        StartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        SurTeam = surTeam;
        HunTeam = hunTeam;
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        GameProgress = gameProgress;
        CurrentHunBannedList.AddRange(Enumerable.Range(0, 2).Select(i => new Character(Camp.Hun)));
        CurrentSurBannedList.AddRange(Enumerable.Range(0, 4).Select(i => new Character(Camp.Sur)));
    }

    /// <summary>
    /// 刷新当前玩家数据绑定
    /// 同步更新求生者玩家列表和监管者玩家对象到队伍的最新状态
    /// </summary>
    public void RefreshCurrentPlayer()
    {
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        OnPropertyChanged();
    }
}