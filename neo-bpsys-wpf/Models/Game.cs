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
    private ObservableCollection<Character?> _currentHunBannedList = new();

    [ObservableProperty]
    private ObservableCollection<Character?> _currentSurBannedList = new();

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
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        GameProgress = gameProgress;
        CurrentHunBannedList.AddRange(Enumerable.Range(0, 2).Select(i => new Character(Camp.Hun)));
        CurrentSurBannedList.AddRange(Enumerable.Range(0, 4).Select(i => new Character(Camp.Sur)));
        OnPropertyChanged(string.Empty);
    }

    public void RefreshCurrentPlayer()
    {
        SurPlayerList = SurTeam.SurPlayerOnFieldList;
        HunPlayer = HunTeam.HunPlayerOnField;
        OnPropertyChanged();
    }
}
