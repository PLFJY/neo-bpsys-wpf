using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public string ImageUri { get; set; } = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Member> _surMemberList = new();

    [ObservableProperty]
    private ObservableCollection<Member> _hunMemberList = new();

    [ObservableProperty]
    private Character[] _currentBannedHunArray;

    [ObservableProperty]
    private Character[] _currentBannedSurArray;

    [ObservableProperty]
    private Character[] _globalBannedSurArray;

    [ObservableProperty]
    private Character[] _globalBannedHunArray;

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

        CurrentBannedHunArray = new Character[2];
        for(int i = 0; i < 2; i++)
        {
            CurrentBannedHunArray[i] = new(Camp.Hun);
        }

        CurrentBannedSurArray = new Character[4];
        for(int i = 0; i < 4; i++)
        {
            CurrentBannedSurArray[i] = new(Camp.Sur);
        }

        GlobalBannedHunArray = new Character[3];
        for(int i= 0; i < 3; i++)
        {
            GlobalBannedHunArray[i] = new(Camp.Hun);
        }

        GlobalBannedSurArray = new Character[9];
        for (int i = 0; i < 9; i++)
        {
            GlobalBannedSurArray[i] = new(Camp.Sur);
        }

        SurPlayerOnFieldArray = new Player[4];
        for (int i = 0; i < 4; i++)
        {
            SurPlayerOnFieldArray[i] = new Player(Camp.Sur, i);
        }
        HunPlayerOnField = new Player(Camp.Hun);
    }

    /// <summary>
    /// Import information to the current Team includes Name, LogoUri, MemberList
    /// </summary>
    /// <param name="newTeam"></param>
    public void ImportTeamInfo(Team newTeam)
    {
        Name = newTeam.Name;
        Logo = null;
        if (!string.IsNullOrEmpty(newTeam.ImageUri))
        {
            ImageUri = newTeam.ImageUri;
            Logo = new BitmapImage(new Uri(ImageUri));
        }
        SurMemberList = newTeam.SurMemberList;
        HunMemberList = newTeam.HunMemberList;
        SurPlayerOnFieldArray = new Player[4];
        for (int i = 0; i < 4; i++)
        {
            SurPlayerOnFieldArray[i] = new Player(Camp.Sur, i);
        }
        HunPlayerOnField = new Player(Camp.Hun);

        OnPropertyChanged();
    }

    /// <summary>
    /// Check can let new member on field
    /// </summary>
    /// <param name="camp"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Add let a member on field
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
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

    /// <summary>
    /// let a member off field
    /// </summary>
    /// <param name="member"></param>
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
