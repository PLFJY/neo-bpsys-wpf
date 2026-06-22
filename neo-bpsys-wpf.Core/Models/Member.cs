using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.String;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 选手类, 注意与 <see cref="Player"/> 类做区分，这是表示上场的选手，本类是表示队伍内的成员, <see cref="Models.Member"/> 被它所操纵的 <see cref="Player"/> 包含
/// </summary>
[FrontedBindingObject]
public partial class Member : ObservableObjectBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="camp"></param>
    public Member(Camp camp)
    {
        Camp = camp;
    }

    private string _name = Empty;

    /// <summary>
    /// 选手名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// 选手所属阵营
    /// </summary>
    [ObservableProperty]
    public partial Camp Camp { get; set; }

    private ImageSource? _image;

    /// <summary>
    /// 选手定妆照
    /// </summary>
    [JsonIgnore]
    public ImageSource? Image
    {
        get
        {
            if (_image != null || ImageUri == null) return _image;
            _image = new BitmapImage(new Uri(ImageUri));
            return _image;
        }
        set => SetPropertyWithAction(ref _image, value, _ =>
        {
            ImageUri = null;
            OnPropertyChanged(nameof(IsImageValid));
        });
    }

    /// <summary>
    /// 选手定妆照的图片 Uri
    /// </summary>
    [FrontedBindingIgnore]
    public string? ImageUri { get; set; }

    /// <summary>
    /// 选手是否上场
    /// </summary>
    [ObservableProperty]
    public partial bool IsOnField { get; set; }

    /// <summary>
    /// 选手是否可上场
    /// </summary>
    [ObservableProperty]
    [JsonIgnore]
    public partial bool CanOnFieldChange { get; set; } = true;

    /// <summary>
    /// 选手定妆照是否有效
    /// </summary>
    [JsonIgnore]
    [FrontedBindingIgnore]
    public bool IsImageValid => Image != null;
}
