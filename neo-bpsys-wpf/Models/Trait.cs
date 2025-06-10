using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Helpers;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 天赋类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
public class Trait
{
    public Enums.Trait? TraitName { get; }
    [JsonIgnore]
    public ImageSource? Image { get; }

    public Trait(Enums.Trait? trait)
    {
        if (trait == null) return;

        Image = ImageHelper.GetImageSourceFromName(Enums.ImageSourceKey.trait, trait.ToString());
    }
}
