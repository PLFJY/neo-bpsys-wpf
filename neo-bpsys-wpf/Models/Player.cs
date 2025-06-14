using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 选手类, 注意与 <see cref="Models.Member"/> 类做区分，这是表示队伍内的成员，本类是表示上场的选手, <see cref="Player"/> 类包含操纵它的 <see cref="Models.Member"/>
/// </summary>
public partial class Player : ObservableRecipient, IRecipient<MemberStateChangedMessage>
{
    public Player()
    {
        IsActive = true;
    }

    public Player(Member member)
    {
        this.Member = member;
        IsActive = true;
    }

    [ObservableProperty]
    private Member _member = new();

    [ObservableProperty]
    private bool _isMemberValid = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PictureShown))]
    private Character? _character;

    [ObservableProperty]
    private Talent _talent = new();

    [ObservableProperty]
    private Trait _trait = new(null);

    [ObservableProperty]
    private PlayerData _data = new();

    [JsonIgnore]
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

    public void Receive(MemberStateChangedMessage message)
    {
        OnPropertyChanged(nameof(PictureShown));
    }
}