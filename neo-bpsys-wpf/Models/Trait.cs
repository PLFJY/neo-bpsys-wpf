using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf;
using neo_bpsys_wpf.Helpers;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public partial class Trait : ObservableObject
{
    public Enums.Trait? TraitName { get; set; }

    [ObservableProperty]
    public ImageSource? _image;

    public Trait(Enums.Trait? trait)
    {
        if (trait == null) return;

        Image = ImageHelper.GetImageSourceFromName(Enums.ImageSourceKey.trait, trait.ToString());
    }
}
