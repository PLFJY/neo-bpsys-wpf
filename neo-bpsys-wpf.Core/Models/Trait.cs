using System.Text.Json.Serialization;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 天赋类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
public class Trait
{
    /// <summary>
    /// 是否启用黑色版本
    /// </summary>
    private readonly bool _isBlackTalentAndTraitEnable;

    /// <summary>
    /// 天赋名称
    /// </summary>
    public Enums.TraitType? TraitName { get; }

    private ImageSource? _image;

    /// <summary>
    /// 天赋图标
    /// </summary>
    [JsonIgnore]
    public ImageSource? Image
    {
        get
        {
            _image ??= ImageHelper.GetTraitImageSource(TraitName, _isBlackTalentAndTraitEnable);
            return _image;
        }
    }

    /// <summary>
    /// JSON 反序列化构造函数
    /// </summary>
    /// <param name="traitName">天赋名称</param>
    [JsonConstructor]
    internal Trait(Enums.TraitType? traitName) : this(traitName, false)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="trait">天赋名称</param>
    /// <param name="isBlackTalentAndTraitEnable">是否启用黑色版本</param>
    public Trait(Enums.TraitType? trait, bool isBlackTalentAndTraitEnable = false)
    {
        _isBlackTalentAndTraitEnable = isBlackTalentAndTraitEnable;
        if (trait == null) return;
        TraitName = trait;
        _image = ImageHelper.GetTraitImageSource(trait, isBlackTalentAndTraitEnable);
    }
}