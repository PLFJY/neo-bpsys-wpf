using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.TeamJsonMaker;

public partial class Team : ObservableObjectBase
{
    public Team()
    {
        for (var i = 0; i < 4; i++)
        {
            SurMemberList.Add(new Member(Camp.Sur));
        }

        HunMemberList.Add(new Member(Camp.Hun));
    }

    public string TeamName { get; set; } = string.Empty;

    /// <summary>
    /// 求生者队员列表
    /// </summary>
    public ObservableCollection<Member> SurMemberList { get; } = [];

    /// <summary>
    /// 监管者队员列表
    /// </summary>
    public ObservableCollection<Member> HunMemberList { get; } = [];

    public string ImageUri { get; set; } = string.Empty;
}