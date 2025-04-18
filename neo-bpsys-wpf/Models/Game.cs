using Force.DeepCloner;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Models;

public class Game
{
    public string GUID { get; set; }

    public string StartTime { get; set; }

    public Team SurTeam { get; set; }

    public Team HunTeam { get; set; }
    public GameProgress GameProgress { get; set; }
    public Map? PickedMap { get; set; }
    public Map? BandedMap { get; set; }
    public Player[] SurPlayerList { get; set; }

    public Player HunPlayer { get; set; }

    public Game(Team surTeam, Team hunTeam, GameProgress gameProgress)
    {
        GUID = Guid.NewGuid().ToString("D");
        StartTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        SurTeam = surTeam.DeepClone() ?? throw new ArgumentNullException(nameof(surTeam));
        HunTeam = hunTeam.DeepClone() ?? throw new ArgumentNullException(nameof(surTeam));
        SurPlayerList = Enumerable.Range(0, 4).Select(index => new Player(Camp.Sur, index)).ToArray();
        HunPlayer = new Player(Camp.Hun);
        GameProgress = gameProgress;
    }
}