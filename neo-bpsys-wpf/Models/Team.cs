using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 队伍类, <see cref="SharedDataService"/> 中主队和客队对应的对象全场始终不变，信息导入依靠 <see cref="ImportTeamInfo(Team)"/> 方法
/// </summary>
public partial class Team : ViewModelBase
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public Team()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

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
        set => SetProperty(ref _name, value);
    }


    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _logo;

    public string ImageUri { get; set; } = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Member> _surMemberList = [];

    [ObservableProperty]
    private ObservableCollection<Member> _hunMemberList = [];

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedSurList = [];

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedHunList = [];

    public Character?[] GlobalBannedSurRecordArray { get; }

    public Character?[] GlobalBannedHunRecordArray { get; }

    [ObservableProperty]
    [property: JsonIgnore]
    private ObservableCollection<Member?> _surMemberOnFieldList = [];

    [ObservableProperty]
    [property: JsonIgnore]
    private Member? _hunMemberOnField;

    [ObservableProperty]
    private Score _score = new();

    private int _onFieldSurPlayerCnt;

    public Team(Camp camp)
    {
        SurMemberList = [.. Enumerable.Range(0, 4).Select(_ => new Member(Camp.Sur))];
        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;

        GlobalBannedHunList = [.. Enumerable.Range(0, 3).Select(_ => new Character(Camp.Hun))];
        GlobalBannedSurList = [.. Enumerable.Range(0, 9).Select(_ => new Character(Camp.Sur))];

        SurMemberOnFieldList = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        HunMemberOnField = null;

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
            MemberOffField(member);
        }
        foreach (var member in HunMemberList)
        {
            MemberOffField(member);
        }
        SurMemberList = newTeam.SurMemberList;
        HunMemberList = newTeam.HunMemberList;

        SurMemberOnFieldList = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        HunMemberOnField = null;

        _onFieldSurPlayerCnt = 0;
        WeakReferenceMessenger.Default.Send(new MemberPropertyChangedMessage(this));
        WeakReferenceMessenger.Default.Send(new MemberOnFieldChangedMessage(this));
        OnPropertyChanged();
    }

    /// <summary>
    /// Check can let new member on field
    /// </summary>
    /// <param name="camp"></param>
    /// <returns></returns>
    public bool CanMemberOnField(Camp camp)
    {
        if (camp == Camp.Hun)
        {
            return HunMemberOnField == null;
        }

        return _onFieldSurPlayerCnt < 4;
    }

    /// <summary>
    /// Add let a member on field
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
    public bool MemberOnField(Member member)
    {
        if (!CanMemberOnField(member.Camp))
            return false;

        if (member.Camp == Camp.Hun)
        {
            HunMemberOnField = member;
        }
        else
        {
            for (var i = 0; i < SurMemberOnFieldList.Count; i++)
            {
                if (SurMemberOnFieldList[i] != null) continue;
                SurMemberOnFieldList[i] = member;
                _onFieldSurPlayerCnt++;
                break;
            }
        }

        WeakReferenceMessenger.Default.Send(new MemberOnFieldChangedMessage(this));
        return true;
    }

    /// <summary>
    /// let a member off field
    /// </summary>
    /// <param name="member"></param>
    public void MemberOffField(Member member)
    {
        if (member.Camp == Camp.Hun)
        {
            if (HunMemberOnField != member) return;
            HunMemberOnField = null;
            WeakReferenceMessenger.Default.Send(new MemberOnFieldChangedMessage(this));
        }
        else
        {
            for (var i = 0; i < SurMemberOnFieldList.Count; i++)
            {
                if (SurMemberOnFieldList[i] != member) continue;
                SurMemberOnFieldList[i] = null;
                _onFieldSurPlayerCnt--;
                WeakReferenceMessenger.Default.Send(new MemberOnFieldChangedMessage(this));
                return;
            }
        }
    }
}
