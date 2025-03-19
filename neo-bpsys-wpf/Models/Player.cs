using System.Collections.Generic;

namespace neo_bpsys_wpf.Models;

public class Player : Member
{
    public Character Character {set; get;}

    public int position { get; set; }
    
    public List<Talent> Talent { get; set; }
    
    public Trait Trait { get; set; }

    public PlayerData Data { get; set; }
}