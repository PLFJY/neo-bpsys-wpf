using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 队伍类, <see cref="neo_bpsys_wpf.Services.SharedDataService"/> 中主队和客队对应的对象全场始终不变，信息导入依靠 <see cref="ImportTeamInfo(Team)"/> 方法
/// </summary>
public partial class Team : ViewModelBase
{
    /// <summary>
    /// 队伍类型(主队/客队)
    /// </summary>
    public TeamType TeamType { get; }
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public Team()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private string _name = string.Empty;

    /// <summary>
    /// 队伍名称
    /// </summary>
    public string Name
    {
        get
        {
#if DEBUG
            return TeamType switch
            {
                TeamType.MainTeam => string.IsNullOrEmpty(_name) ? "主队" : _name,
                TeamType.AwayTeam => string.IsNullOrEmpty(_name) ? "客队" : _name,
                _ => _name
            };
#endif
            return _name;
        }
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// 队伍阵营
    /// </summary>
    [ObservableProperty] private Camp _camp;

    /// <summary>
    /// 残留的屎山，后续利用Converter实现，这里不应该出现，因为是Windows特征代码
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _logo;

    /// <summary>
    /// 队伍LOGO的Uri
    /// </summary>
    public string ImageUri { get; set; } = string.Empty;

    private ObservableCollection<Member> _surMemberList = [];

    /// <summary>
    /// 求生者队员列表
    /// </summary>
    public ObservableCollection<Member> SurMemberList
    {
        get => _surMemberList;
        private set => SetProperty(ref _surMemberList, value);
    }

    private ObservableCollection<Member> _hunMemberList = [];

    /// <summary>
    /// 监管者队员列表
    /// </summary>
    public ObservableCollection<Member> HunMemberList
    {
        get => _hunMemberList;
        private set => SetProperty(ref _hunMemberList, value);
    }

    /// <summary>
    /// 全局被禁用的求生者列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<Character?> _globalBannedSurList = [];

    /// <summary>
    /// 全局被禁用的监管者列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<Character?> _globalBannedHunList = [];

    /// <summary>
    /// 全局被禁用的求生者记录
    /// </summary>
    public Character?[] GlobalBannedSurRecordArray { get; }

    /// <summary>
    /// 全局被禁用的监管者记录
    /// </summary>
    public Character?[] GlobalBannedHunRecordArray { get; }

    [JsonIgnore]
    private ObservableCollection<Member?> _surMemberOnFieldPrivateCollection = [];

    private ReadOnlyObservableCollection<Member?> _surMemberOnFieldCollection;
    /// <summary>
    /// 正在场上的求生者队员列表
    /// </summary>
    [JsonIgnore]
    public ReadOnlyObservableCollection<Member?> SurMemberOnFieldCollection
    {
        get => _surMemberOnFieldCollection;
        private set => SetProperty(ref _surMemberOnFieldCollection, value);
    }

    private Member? _hunMemberOnField;

    /// <summary>
    /// 正在场上的监管者队员
    /// </summary>
    [JsonIgnore]
    public Member? HunMemberOnField
    {
        get => _hunMemberOnField;
        private set => SetProperty(ref _hunMemberOnField, value);
    }

    /// <summary>
    /// 队伍比分
    /// </summary>
    public Score Score { get; private set; } = new();

    /// <summary>
    /// 队伍目前场上的求生者数量
    /// </summary>
    private int _onFieldSurPlayerCnt;


    /// <summary>
    /// 队伍目前场上的求生者数量
    /// </summary>
    /// <param name="camp">队伍阵营</param>
    /// <param name="teamType">队伍类型</param>
    public Team(Camp camp, TeamType teamType)
    {
        TeamType = teamType;
        SurMemberList = [.. Enumerable.Range(0, 4).Select(_ => new Member(Camp.Sur))];
        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;

        GlobalBannedHunList = [.. Enumerable.Range(0, 3).Select(_ => new Character(Camp.Hun))];
        GlobalBannedSurList = [.. Enumerable.Range(0, 9).Select(_ => new Character(Camp.Sur))];

        _surMemberOnFieldPrivateCollection = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        _surMemberOnFieldCollection = new ReadOnlyObservableCollection<Member?>(_surMemberOnFieldPrivateCollection);
        OnPropertyChanged(nameof(SurMemberOnFieldCollection));
        HunMemberOnField = null;

        GlobalBannedSurRecordArray = [.. Enumerable.Range(0, 9).Select<int, Character?>(_ => null)];
        GlobalBannedHunRecordArray = [.. Enumerable.Range(0, 3).Select<int, Character?>(_ => null)];
    }

    /// <summary>
    /// 导入队伍信息，包括队伍名字、队标Uri、选手列表
    /// </summary>
    /// <param name="newTeam">队伍信息</param>
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

        _surMemberOnFieldPrivateCollection = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        SurMemberOnFieldCollection = new(_surMemberOnFieldPrivateCollection);
        HunMemberOnField = null;

        _onFieldSurPlayerCnt = 0;
    }

    #region 选手操作

    /// <summary>
    /// 检查是否允许队员上场
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <returns>是否允许</returns>
    public bool CanMemberOnField(Camp camp)
    {
        if (camp == Camp.Hun)
        {
            return HunMemberOnField == null;
        }

        return _onFieldSurPlayerCnt < 4;
    }

    /// <summary>
    /// 添加一名上场选手
    /// </summary>
    /// <param name="member">选手</param>
    /// <returns>是否成功</returns>
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
            for (var i = 0; i < _surMemberOnFieldPrivateCollection.Count; i++)
            {
                if (_surMemberOnFieldPrivateCollection[i] != null) continue;
                _surMemberOnFieldPrivateCollection[i] = member;
                _onFieldSurPlayerCnt++;
                break;
            }
        }
        MemberOnFieldChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 移除一名上场选手
    /// </summary>
    /// <param name="member">选手</param>
    public void MemberOffField(Member member)
    {
        if (member.Camp == Camp.Hun)
        {
            if (HunMemberOnField != member) return;
            HunMemberOnField = null;
        }
        else
        {
            for (var i = 0; i < _surMemberOnFieldPrivateCollection.Count; i++)
            {
                if (_surMemberOnFieldPrivateCollection[i] != member) continue;
                _surMemberOnFieldPrivateCollection[i] = null;
                _onFieldSurPlayerCnt--;
                return;
            }
        }
        MemberOnFieldChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? MemberOnFieldChanged;

    #endregion

    /// <summary>
    /// 重置比分
    /// </summary>
    public void ResetScore() => Score = new Score();
}