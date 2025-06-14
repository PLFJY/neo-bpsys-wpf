using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 对局类, 创建需要导入 <see cref="SurTeam"/> 和 <see cref="HunTeam"/> 两支队伍以及对局进度
/// </summary>
public partial class Game : ObservableObject
{
    public Guid GUID { get; }

    public string StartTime { get; }

    [ObservableProperty]
    private Team _surTeam = new(Camp.Sur);

    [ObservableProperty]
    private Team _hunTeam = new(Camp.Hun);

    [ObservableProperty]
    private GameProgress _gameProgress;

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

    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _pickedMapImage;

    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _bannedMapImage;

    [ObservableProperty]
    private ObservableCollection<Character?> _currentHunBannedList = [];

    [ObservableProperty]
    private ObservableCollection<Character?> _currentSurBannedList = [];

    [ObservableProperty]
    private ObservableCollection<Player> _surPlayerList;

    [ObservableProperty]
    private Player _hunPlayer;

    public Game(Team surTeam, Team hunTeam, GameProgress gameProgress)
    {
        GUID = Guid.NewGuid();
        StartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        SurTeam = surTeam;
        HunTeam = hunTeam;
        //重置角色选择
        for (int i = 0; i < SurTeam.SurPlayerOnFieldList.Count; i++)
        {
            SurTeam.SurPlayerOnFieldList[i] = new(SurTeam.SurPlayerOnFieldList[i].Member);
        }
        SurTeam.HunPlayerOnField = new(SurTeam.HunPlayerOnField.Member);
        for (int i = 0; i < HunTeam.SurPlayerOnFieldList.Count; i++)
        {
            HunTeam.SurPlayerOnFieldList[i] = new(HunTeam.SurPlayerOnFieldList[i].Member);
        }
        HunTeam.HunPlayerOnField = new(HunTeam.HunPlayerOnField.Member);
        //刷新上场列表
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        GameProgress = gameProgress;
        //新建角色禁用列表
        CurrentHunBannedList = [.. Enumerable.Range(0, 2).Select(i => new Character(Camp.Hun))];
        CurrentSurBannedList = [.. Enumerable.Range(0, 4).Select(i => new Character(Camp.Sur))];
        OnPropertyChanged(nameof(SurTeam));
        OnPropertyChanged(nameof(HunTeam));
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// 刷新当前上场选手列表
    /// </summary>
    public void RefreshCurrentPlayer()
    {
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        OnPropertyChanged();
    }
}
