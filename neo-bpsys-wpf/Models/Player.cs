using Microsoft.Extensions.Options;
using neo_bpsys_wpf.Enums;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public class Player
{
    public Player(Camp camp, int? position = null)
    {
        Camp = camp;
        Character = new Character(camp);
        Position = position;
    }

    public Player(Camp camp, string name, BitmapImage image, bool isPlaying)
    {
        Character = new Character(camp);
    }

    public string Name { get; set; } = string.Empty;

    public Camp Camp { get; set; }

    public Character Character { set; get; }

    public int? Position { get; set; }

    public BitmapImage? Image { get; set; }

    public List<Talent>? Talent { get; set; }

    public Trait? Trait { get; set; }

    public PlayerData? Data { get; set; }
}