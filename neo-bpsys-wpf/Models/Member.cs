using neo_bpsys_wpf.Enums;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public class Member
{
    public Camp Camp { get; set; }

    public string Name { get; set; } = string.Empty;

    public BitmapImage? Image { get; set; }

    public bool IsPlaying { get; set; } = false;

}