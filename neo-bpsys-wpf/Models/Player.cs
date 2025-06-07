using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// ѡ����, ע���� <see cref="Models.Member"/> �������֣����Ǳ�ʾ�����ڵĳ�Ա�������Ǳ�ʾ�ϳ���ѡ��, <see cref="Player"/> ������������� <see cref="Models.Member"/>
/// </summary>
public partial class Player : ObservableObject
{
    public Player()
    {
        //this.Character = new Character(camp);
    }

    public Player(Member member)
    {
        this.Member = member;
        //this.Character = new Character(member.Camp);
    }

    [ObservableProperty]
    private Member _member = new();

    [ObservableProperty]
    private bool _isMemberValid = false;

    private Character? _character;
    public Character? Character
    {
        get => _character;
        set
        {
            _character = value;
            OnPropertyChanged(nameof(Character));
            OnPropertyChanged(nameof(PictureShown));
        }
    }

    [ObservableProperty]
    private Talent _talent = new();

    [ObservableProperty]
    private Trait _trait = new(null);

    [ObservableProperty]
    private PlayerData _data = new();
    

    public ImageSource? PictureShown
    {
        get
        {
            if (Character == null)
            {
                return Member.Image;
            }
            else
            {
                return Character.HalfImage;
            }
        }
    }
}