using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 选手类, 注意与 <see cref="Models.Member"/> 类做区分，这是表示队伍内的成员，本类是表示上场的选手, <see cref="Player"/> 类包含操纵它的 <see cref="Models.Member"/>
/// </summary>
public partial class Player : ObservableObjectBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="member">选手</param>
    public Player(Member member)
    {
        Member = member;
    }

    /// <summary>
    /// 操控的选手
    /// </summary>
    [ObservableProperty][NotifyPropertyChangedFor(nameof(PictureShown))] private Member _member;

    /// <summary>
    /// 选手所选的角色
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PictureShown))]
    private Character? _character;

    /// <summary>
    /// 天赋
    /// </summary>
    [ObservableProperty] private Talent _talent = new();

    /// <summary>
    /// 辅助特质
    /// </summary>
    [ObservableProperty] private Trait _trait = new(null);

    /// <summary>
    /// 选手的数据
    /// </summary>
    [ObservableProperty] private PlayerData _data = new();

    /// <summary>
    /// 显示的图片
    /// </summary>
    [JsonIgnore] public ImageSource? PictureShown => Character == null ? Member.Image : Character?.BigImage;
}