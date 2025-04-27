using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;

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

    [ObservableProperty]
    [JsonIgnore]
    private Player[] _surPlayerOnFieldArray;

    [ObservableProperty]
    [JsonIgnore]
    private Player _hunPlayerOnField;

    public Score Score { get; set; } = new Score();

    public Team(Camp camp)
    {
        for (int i = 0; i < 4; i++)
        {
            SurMemberList.Add(new Member(Camp.Sur));
        }

        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;

        SurPlayerOnFieldArray = new Player[4];
        for (int i = 0; i < 4; i++)
        {
            SurPlayerOnFieldArray[i] = new Player(Camp.Sur, i);
        }
        HunPlayerOnField = new Player(Camp.Hun);
    }

    public void ImportTeamInfo(Team newTeam)
    {
        Name = newTeam.Name;
        Logo = null;
        SurMemberList = newTeam.SurMemberList;
        HunMemberList = newTeam.HunMemberList;
        if (newTeam.SurGlobalBanList != null)
            SurGlobalBanList = newTeam.SurGlobalBanList;
        else
            SurGlobalBanList = new();
        if (newTeam.HunGlobalBanList != null)
            HunGlobalBanList = newTeam.HunGlobalBanList;
        else
            HunGlobalBanList = new();
        SurPlayerOnFieldArray = new Player[4];
        for (int i = 0; i < 4; i++)
        {
            SurPlayerOnFieldArray[i] = new Player(Camp.Sur, i);
        }
        HunPlayerOnField = new Player(Camp.Hun);

        OnPropertyChanged();
    }

    public bool CanAddMemberInPlayer(Camp camp)
    {
        if (camp == Camp.Hun)
        {
            return !HunPlayerOnField.IsMemberValid;
        }

        foreach (var p in SurPlayerOnFieldArray)
        {
            if (!p.IsMemberValid)
                return true;
        }
        return false;
    }

    public bool AddMemberInPlayer(Member member)
    {
        if (!CanAddMemberInPlayer(member.Camp))
            return false;

        if (member.Camp == Camp.Hun)
        {
            HunPlayerOnField.Member = member;
            HunPlayerOnField.IsMemberValid = true;
            return true;
        }

        foreach (var p in SurPlayerOnFieldArray)
        {
            if (!p.IsMemberValid)
            {
                p.Member = member;
                p.IsMemberValid = true;
                break;
            }
        }

        return true;
    }

    public void RemoveMemberInPlayer(Member member)
    {
        if (member.Camp == Camp.Hun)
        {
            HunPlayerOnField.Member = new(Camp.Hun);
            HunPlayerOnField.IsMemberValid = false;
            return;
        }

        foreach (var p in SurPlayerOnFieldArray)
        {
            if (p.Member == member)
            {
                p.Member = new(Camp.Sur);
                p.IsMemberValid = false;
                return;
            }
        }
    }
}
