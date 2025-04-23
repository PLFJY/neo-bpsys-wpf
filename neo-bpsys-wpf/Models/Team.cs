using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

public partial class Team : ObservableObject
{
    public Team()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    private ImageSource? _logo;

    [ObservableProperty]
    private ObservableCollection<Member> _surMemberList = new();

    [ObservableProperty]
    private ObservableCollection<Member> _hunMemberList = new();

    [ObservableProperty]
    private ObservableCollection<Character> _surGlobalBanList = new();

    [ObservableProperty]
    private ObservableCollection<Character> _hunGlobalBanList = new();

    public Score Score { get; set; } = new Score();

    public Team(Camp camp)
    {
        for (int i = 0; i < 4; i++)
        {
            SurMemberList.Add(new Member(Camp.Sur));
        }

        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;
    }
}
