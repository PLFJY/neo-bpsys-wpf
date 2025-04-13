namespace neo_bpsys_wpf.Models;

public class Game
{
    public string GUID { get; set; }

    public string StartTime { get; set; }

    public Team SurTeam { get; set; }

    public Team HunTeam { get; set; }

    public List<Player> SurPlayerList { get; set; }

    public Player HunPlayer { get; set; }

    public Game(Team surTeam, Team hunTeam, List<Player> surPlayerList, Player hunPlayer)
    {
        GUID = Guid.NewGuid().ToString("D");
        StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        SurTeam = surTeam ?? throw new ArgumentNullException(nameof(surTeam));
        HunTeam = hunTeam ?? throw new ArgumentNullException(nameof(hunTeam));
        SurPlayerList = surPlayerList ?? throw new ArgumentNullException(nameof(surPlayerList));
        HunPlayer = hunPlayer ?? throw new ArgumentNullException(nameof(hunPlayer));
    }
}