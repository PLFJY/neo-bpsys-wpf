using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys.Core.Enums;

namespace neo_bpsys.Core.Models;

public partial class Game : ObservableObject
{
    public Game(Team surTeam, Team hunTeam, GameProgress progress)
    {
        SurTeam = surTeam;
        HunTeam = hunTeam;
        Progress = progress;
    }

    public Team SurTeam { get; }
    public Team HunTeam { get; }
    [ObservableProperty] private GameProgress _progress;
}

佣兵   机械师   盲女   机械师   破伦   爱哭鬼