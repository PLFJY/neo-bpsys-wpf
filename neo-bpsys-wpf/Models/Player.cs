using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public partial class Player : ObservableObject
{
    public Player(Camp camp, int? position = null)
    {
        Character = new Character(camp);
        Position = position;
    }

    public Player(Camp camp, string name, BitmapImage image)
    {
        Character = new Character(camp);
    }

    [ObservableProperty]
    private Member _member = new();

    [ObservableProperty]
    private bool _isMemberValid = false;

    [ObservableProperty]
    private Character _character;

    [ObservableProperty]
    private int? _position;

    [ObservableProperty]
    private ObservableCollection<Talent>? _talent;

    [ObservableProperty]
    private Trait? _trait;

    [ObservableProperty]
    private PlayerData? _data;
}
