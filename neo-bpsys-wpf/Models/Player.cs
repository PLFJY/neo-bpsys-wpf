using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using System.Text.Json.Serialization;
using System.Windows.Media;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 选手类, 注意与 <see cref="Models.Member"/> 类做区分，这是表示队伍内的成员，本类是表示上场的选手, <see cref="Player"/> 类包含操纵它的 <see cref="Models.Member"/>
/// </summary>
public partial class Player : ViewModelBase, IRecipient<MemberPropertyChangedMessage>, IRecipient<MemberOnFieldChangedMessage>, IRecipient<SwapMessage>
{
    public Player(Member member, bool isMemberValid)
    {
        Member = member;
        IsActive = true;
        IsMemberValid = isMemberValid;
    }

    [ObservableProperty] private Member _member;

    public bool IsMemberValid { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PictureShown))]
    private Character? _character;

    [ObservableProperty] private Talent _talent = new();

    [ObservableProperty] private Trait _trait = new(null);

    [ObservableProperty] private PlayerData _data = new();

    [JsonIgnore] public ImageSource? PictureShown => Character == null ? Member?.Image : Character.HalfImage;

    public void Receive(MemberPropertyChangedMessage message) => OnPropertyChanged(nameof(PictureShown));
    public void Receive(MemberOnFieldChangedMessage message) => OnPropertyChanged(nameof(PictureShown));
    public void Receive(SwapMessage message)
    {
        if(message.IsSwapped)
            OnPropertyChanged(nameof(PictureShown));
    }
}