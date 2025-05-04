using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

public partial class Team : ObservableObject
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public Team()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
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
    private ObservableCollection<Character?> _globalBannedSurList = new();

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedHunList = new();

    public Character?[] GlobalBannedSurRecordArray { get; set; } = new Character[9];

    public Character?[] GlobalBannedHunRecordArray { get; set; } = new Character[3];

    [ObservableProperty]
    [JsonIgnore]
    private ObservableCollection<Player> _surPlayerOnFieldList = new();

    [ObservableProperty]
    [JsonIgnore]
    private Player _hunPlayerOnField;

    public Score Score { get; set; } = new Score();

    public Team(Camp camp)
    {
        SurMemberList.AddRange(Enumerable.Repeat(new Member(Camp.Sur), 4));
        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;

        GlobalBannedHunList.AddRange(Enumerable.Repeat(new Character(Camp.Hun), 3));
        GlobalBannedSurList.AddRange(Enumerable.Repeat(new Character(Camp.Sur), 9));

        SurPlayerOnFieldList.AddRange(Enumerable.Range(0, 4)
            .Select(i => new Player(Camp.Sur, i))
            );
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
        SurPlayerOnFieldList.AddRange(Enumerable.Range(0, 4)
            .Select(i => new Player(Camp.Sur, i))
            );
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

        foreach (var p in SurPlayerOnFieldList)
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

        foreach (var p in SurPlayerOnFieldList)
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

        foreach (var p in SurPlayerOnFieldList)
        {
            if (p.Member == member)
            {
                p.Member = new(Camp.Sur);
                p.IsMemberValid = false;
                return;
            }
        }
    }

    public void SyncGlobalBanWithRecord()
    {
        GlobalBannedSurList = new(GlobalBannedSurRecordArray.ToList());
        GlobalBannedHunList = new(GlobalBannedHunRecordArray.ToList());
    }
}
