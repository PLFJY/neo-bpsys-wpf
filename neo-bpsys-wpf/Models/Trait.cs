using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf;
using neo_bpsys_wpf.Helpers;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示角色特质的模型类，继承自ObservableObject以支持MVVM模式下的属性通知
/// </summary>
public partial class Trait : ObservableObject
{
    /// <summary>
    /// 获取或设置特质名称（可空）
    /// </summary>
    public Enums.Trait? TraitName { get; set; }

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置特质对应的图像资源
    /// </summary>
    [ObservableProperty]
    public ImageSource? _image;

    /// <summary>
    /// 初始化一个新特性实例并加载相应的图像资源
    /// </summary>
    /// <param name="trait">要初始化的特质</param>
    public Trait(Enums.Trait? trait)
    {
        if (trait == null) return;

        Image = ImageHelper.GetImageSourceFromName(Enums.ImageSourceKey.trait, trait.ToString());
    }
}
