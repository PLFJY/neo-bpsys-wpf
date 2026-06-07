using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using System.Collections.ObjectModel;

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

    private string _colorHex = "#FF337FB9";

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (ColorHelper.TryNormalizeHex(value, out var normalized))
                SetProperty(ref _colorHex, normalized);
        }
    }

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
