namespace neo_bpsys_wpf.Models;

public class Game
{
    public Team SurTeam { get; set; }

    public Team HunTeam { get; set; }

    public List<Player> SurPlayerList { get; set; }

    public Player HunPlayer { get; set; }
}