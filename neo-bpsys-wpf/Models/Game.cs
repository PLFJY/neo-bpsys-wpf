using AutoMapper.QueryableExtensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Force.DeepCloner;
using neo_bpsys_wpf.Enums;
using System;
using System.Diagnostics;

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

    [ObservableProperty]
    private Map? _pickedMap;
    [ObservableProperty]
    private Map? _bandedMap;

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
        for (int i = 0; i < 4; i++)
        {
            SurPlayerArray[i] = new Player(Camp.Sur, i);
        }
        HunPlayer = new Player(Camp.Hun);
        GameProgress = gameProgress;
    }

    
}
