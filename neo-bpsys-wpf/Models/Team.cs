using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public class Team
{
    public string Name { get; set; } = string.Empty;

    public BitmapImage? Logo { get; set; }

    public List<Member> MemberList { get; set; } = new();

    public List<Character> SurGlobalBanList { get; set; } = new();

    public List<Character> HunGlobalBanList { get; set; } = new();

    public Score Score { get; set; } = new Score();
}