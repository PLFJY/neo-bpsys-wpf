using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Helpers;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// �츳��, �����趨���ɹ��캯����ɣ������ں����޸�
/// </summary>
public class Trait
{
    public Enums.Trait? TraitName { get; }

    public ImageSource? Image { get; }

    public Trait(Enums.Trait? trait)
    {
        if (trait == null) return;

        Image = ImageHelper.GetImageSourceFromName(Enums.ImageSourceKey.trait, trait.ToString());
    }
}
