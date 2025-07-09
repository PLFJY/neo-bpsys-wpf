using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 对局类, 创建需要导入 <see cref="SurTeam"/> 和 <see cref="HunTeam"/> 两支队伍以及对局进度
/// </summary>
public partial class Game : ObservableRecipient, IRecipient<MemberOnFieldChangedMessage>, IRecipient<SwapMessage>
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

    public void Receive(SwapMessage message) => LoadMembers();
}