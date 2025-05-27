using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

public partial class Game : ObservableObject
{
    public Guid GUID { get; set; }

    public string StartTime { get; set; }

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
    [JsonIgnore]
    private ImageSource? _pickedMapImage;

    [ObservableProperty]
    [JsonIgnore]
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
        //���ý�ɫѡ��
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
        //ˢ���ϳ��б�
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        GameProgress = gameProgress;
        //�½���ɫ�����б�
        CurrentHunBannedList = [.. Enumerable.Range(0, 2).Select(i => new Character(Camp.Hun))];
        CurrentSurBannedList = [.. Enumerable.Range(0, 4).Select(i => new Character(Camp.Sur))];
        OnPropertyChanged(nameof(SurTeam));
        OnPropertyChanged(nameof(HunTeam));
        OnPropertyChanged(string.Empty);
    }

    public void RefreshCurrentPlayer()
    {
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        OnPropertyChanged();
    }
}
