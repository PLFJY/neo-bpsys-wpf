using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Models;

public partial class Player : ObservableObject
{
    public Player(Camp camp, int? position = null)
    {
        this.Character = new Character(camp);
    }

    public Player(Camp camp, string name, BitmapImage image)
    {
        this.Character = new Character(camp);
    }

    [ObservableProperty]
    private Member _member = new();

    [ObservableProperty]
    private bool _isMemberValid = false;

    [ObservableProperty]
    private Character? _character;

    [ObservableProperty]
    private Talent _talent = new();

    [ObservableProperty]
    private Trait _trait = new(null);

    [ObservableProperty]
    private PlayerData? _data;
}
