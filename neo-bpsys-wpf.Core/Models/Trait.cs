using System.Text.Json.Serialization;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 天赋类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
public class Trait
{
    private readonly bool _isBlackTalentAndTraitEnable;
    private ImageSource? _image;

    public Core.Enums.Trait? TraitName { get; }

    [JsonIgnore]
    public ImageSource? Image
    {
        get
        {
            _image ??= ImageHelper.GetTraitImageSource(TraitName, _isBlackTalentAndTraitEnable);
            return _image;
        }
    }

    public Trait(Core.Enums.Trait? trait, bool isBlackTalentAndTraitEnable = false)
    {
        _isBlackTalentAndTraitEnable = isBlackTalentAndTraitEnable;
        if (trait == null) return;
        _image = ImageHelper.GetTraitImageSource(trait, isBlackTalentAndTraitEnable);
    }
}
