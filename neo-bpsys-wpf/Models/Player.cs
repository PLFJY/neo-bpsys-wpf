using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

public partial class Player : ObservableObject
{
    public Player(Camp camp)
    {
        this.Character = new Character(camp);
    }

    public Player(Member member)
    {
        this.Member = member;
        this.Character = new Character(member.Camp);
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
