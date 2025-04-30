using System;
using System.Diagnostics;
using System.Security.Policy;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Force.DeepCloner;
using neo_bpsys_wpf.Enums;
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
        get { return _pickedMap; }
        set
        {
            _pickedMap = value;
            PickedMapImage = ImageHelper.GetCharacterImageSource(ImageSourceKey.map, _pickedMap.ToString());
            OnPropertyChanged();
        }
    }


    private Map? _bannedMap;

    public Map? BannedMap
    {
        get { return _bannedMap; }
        set
        {
            _bannedMap = value;
            BannedMapImage = ImageHelper.GetCharacterImageSource(ImageSourceKey.map_singleColor, _bannedMap.ToString());
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
    private Player[] _surPlayerArray;

    [ObservableProperty]
    private Player _hunPlayer;

    public Game(Team surTeam, Team hunTeam, GameProgress gameProgress)
    {
        GUID = Guid.NewGuid();
        StartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        SurTeam = surTeam;
        HunTeam = hunTeam;
        SurPlayerArray = new Player[4];
        SurPlayerArray = SurTeam.SurPlayerOnFieldArray;
        HunPlayer = HunTeam.HunPlayerOnField;
        GameProgress = gameProgress;
    }

    public void RefreshCurrentPlayer()
    {
        SurPlayerArray = SurTeam.SurPlayerOnFieldArray;
        HunPlayer = HunTeam.HunPlayerOnField;
        OnPropertyChanged();
    }
}
