using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 天赋类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
public class Trait
{
    private ImageSource? _image;

    public Enums.Trait? TraitName { get; }

    [JsonIgnore]
    public ImageSource? Image
    {
        get
        {
            _image ??= ImageHelper.GetImageSourceFromName(ImageSourceKey.trait, TraitName.ToString());
            return _image;
        }
    }

    public Trait(Enums.Trait? trait)
    {
        if (trait == null) return;
        TraitName = trait;
    }
}
