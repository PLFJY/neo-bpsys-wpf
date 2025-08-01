using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 选手类, 注意与 <see cref="Models.Member"/> 类做区分，这是表示队伍内的成员，本类是表示上场的选手, <see cref="Player"/> 类包含操纵它的 <see cref="Models.Member"/>
/// </summary>
public partial class Player : ViewModelBase
{
    public Player(Member member, bool isMemberValid)
    {
        Member = member;
        IsMemberValid = isMemberValid;
    }

    [ObservableProperty][NotifyPropertyChangedFor(nameof(PictureShown))] private Member _member;

    public bool IsMemberValid { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PictureShown))]
    private Character? _character;

    [ObservableProperty] private Talent _talent = new();

    [ObservableProperty] private Trait _trait = new(null);

    [ObservableProperty] private PlayerData _data = new();

    [JsonIgnore] public ImageSource? PictureShown => Character == null ? Member.Image : Character?.HalfImage;
}