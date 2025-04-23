using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

public partial class Member : ObservableObject
{
    public Member()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    private ImageSource? _image;

    [ObservableProperty]
    private bool _isOnField = false;

    public Member(Camp camp)
    {
        Camp = camp;
    }
}
