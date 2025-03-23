using neo_bpsys_wpf.Enums;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public class Character
{
    public string Name { get; set; } = string.Empty;
    public Types Type { get; set; }

    public string SearchingName { get; set; } = string.Empty;

    public string ImageFileName { get; set; } = string.Empty;

    public BitmapImage? HeaderImage { get; set; }

    public BitmapImage? HeaderBanImage { get; set; }

    public BitmapImage? HalfImage { get; set; }

    public BitmapImage? FullImage { get; set; }

    public Character()
    {

    }

    public Character(Types type)
    {
        Type = type;
    }

}