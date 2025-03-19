using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public class Team
{
    public string Name { get; set; } 

    public BitmapImage Logo { get; set; }

    public List<Member> MemberList { get; set; } = new List<Member>();
    
    public List<Character> SurGlobalBanList { get; set;} = new List<Character>();
    
    public List<Character> HunGlobalBanList { get; set;} = new List<Character>();
    
    public Score Score { get; set; } = new Score();
}