using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 队伍类, <see cref="SharedDataService"/> 中主队和客队对应的对象全场始终不变，信息导入依靠 <see cref="ImportTeamInfo(Team)"/> 方法
/// </summary>
public partial class Team : ObservableObject
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public Team()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    #region 基本信息
    private string _name = string.Empty;

    public string Name
    {
        get
        {
#if DEBUG
            if (this == App.Services.GetRequiredService<ISharedDataService>().MainTeam)
            {
                return string.IsNullOrEmpty(_name) ? "主队" : _name;
            }
            if (this == App.Services.GetRequiredService<ISharedDataService>().AwayTeam)
            {
                return string.IsNullOrEmpty(_name) ? "客队" : _name;
            }
#endif
            return _name;
        }
        set
        {
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }


    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _logo;

    public string ImageUri { get; set; } = string.Empty;
    #endregion 基本信息

    [ObservableProperty]
    private ObservableCollection<Member> _surMemberList = [];

    [ObservableProperty]
    private ObservableCollection<Member> _hunMemberList = [];

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedSurList = [];

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedHunList = [];

    public Character?[] GlobalBannedSurRecordArray { get; set; }

    public Character?[] GlobalBannedHunRecordArray { get; set; }

    [ObservableProperty]
    [property: JsonIgnore]
    private ObservableCollection<Player> _surPlayerOnFieldList = [];

    [ObservableProperty]
    [property: JsonIgnore]
    private Player _hunPlayerOnField;

    [ObservableProperty]
    private Score _score = new();

    private int _onFieldSurPlayerCnt = 0;

    public Team(Camp camp)
    {
        SurMemberList = [.. Enumerable.Range(0, 4).Select(i => new Member(Camp.Sur))];
        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;

        GlobalBannedHunList = [.. Enumerable.Range(0, 3).Select(i => new Character(Camp.Hun))];
        GlobalBannedSurList = [.. Enumerable.Range(0, 9).Select(i => new Character(Camp.Sur))];

        SurPlayerOnFieldList = [.. Enumerable.Range(0, 4).Select(i => new Player())];
        HunPlayerOnField = new Player();

        GlobalBannedSurRecordArray = [.. Enumerable.Range(0, 9).Select<int, Character?>(i => null)];
        GlobalBannedHunRecordArray = [.. Enumerable.Range(0, 3).Select<int, Character?>(i => null)];
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
        foreach (var member in SurMemberList)
        {
            RemoveMemberInPlayer(member);
        }
        foreach (var member in HunMemberList)
        {
            RemoveMemberInPlayer(member);
        }
        SurMemberList = newTeam.SurMemberList;
        HunMemberList = newTeam.HunMemberList;
        SurPlayerOnFieldList = [.. Enumerable.Range(0, 4).Select(i => new Player())];
        HunPlayerOnField = new Player();
        _onFieldSurPlayerCnt = 0;
        WeakReferenceMessenger.Default.Send(new MemberStateChangedMessage(this));
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

        if (_onFieldSurPlayerCnt < 4) return true;

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
        }
        else
        {
            foreach (var p in SurPlayerOnFieldList)
            {
                if (!p.IsMemberValid)
                {
                    p.Member = member;
                    p.IsMemberValid = true;
                    _onFieldSurPlayerCnt++;
                    break;
                }
            }
        }
        WeakReferenceMessenger.Default.Send(new MemberStateChangedMessage(this));
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
        }
        else
        {
            foreach (var p in SurPlayerOnFieldList)
            {
                if (p.Member == member)
                {
                    p.Member = new(Camp.Sur);
                    p.IsMemberValid = false;
                    _onFieldSurPlayerCnt--;
                    break;
                }
            }
        }
        WeakReferenceMessenger.Default.Send(new MemberStateChangedMessage(this));
    }
}
