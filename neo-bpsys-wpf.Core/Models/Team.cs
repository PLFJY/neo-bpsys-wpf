using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 队伍类, <see cref="ISharedDataService"/> 中主队和客队对应的对象全场始终不变，信息导入依靠 <see cref="ImportTeamInfo(Team)"/> 方法
/// </summary>
public partial class Team : ObservableObjectBase
{
    /// <summary>
    /// 队伍类型(主队/客队)
    /// </summary>
    public TeamType TeamType { get; }

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
                TeamType.HomeTeam => string.IsNullOrEmpty(_name) ? "主队" : _name,
                TeamType.AwayTeam => string.IsNullOrEmpty(_name) ? "客队" : _name,
                _ => _name
            };
#endif
            return _name;
        }
        set => SetProperty(ref _name, value);
    }

    private Camp? _camp;

    /// <summary>
    /// 队伍阵营
    /// </summary>
    public Camp? Camp
    {
        get => _camp;
        set => SetPropertyWithAction(ref _camp, value, 
            _ => UpdateGlobalBanFromRecord());
    }

    /// <summary>
    /// 队伍LOGO
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
    [Obsolete("此数组已弃用，将在3.0.0.0后删除，请迁移至 GlobalBannedSurRecordList")]
    [JsonIgnore] public Character?[] GlobalBannedSurRecordArray => [.. GlobalBannedSurRecordList];

    /// <summary>
    /// 全局被禁用的监管者记录
    /// </summary>
    [Obsolete("此数组已弃用，将在3.0.0.0后删除，请迁移至 GlobalBannedHunRecordList")]
    [JsonIgnore] public Character?[] GlobalBannedHunRecordArray => [.. GlobalBannedHunRecordList];

    /// <summary>
    /// 全局被禁用的求生者记录
    /// </summary>
    public ObservableCollection<Character?> GlobalBannedSurRecordList { get; }

    /// <summary>
    /// 全局被禁用的监管者记录
    /// </summary>
    public ObservableCollection<Character?> GlobalBannedHunRecordList { get; }

    /// <summary>
    /// 同步全局禁选记录
    /// </summary>
    public void UpdateGlobalBanFromRecord()
    {
        for (var i = 0; i < GlobalBannedSurRecordList.Count; i++)
        {
            if (GlobalBannedSurRecordList[i] == null) continue;
            GlobalBannedSurList[i] = GlobalBannedSurRecordList[i];
        }

        for (var i = 0; i < GlobalBannedHunRecordList.Count; i++)
        {
            if (GlobalBannedHunRecordList[i] == null) continue;
            GlobalBannedHunList[i] = GlobalBannedHunRecordList[i];
        }
    }

    [JsonIgnore] private ObservableCollection<Member?> _surMemberOnFieldPrivateCollection;

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
    public Score Score { get; } = new();

    /// <summary>
    /// 队伍目前场上的求生者数量
    /// </summary>
    private int _onFieldSurPlayerCnt;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="camp">队伍阵营</param>
    /// <param name="teamType">队伍类型</param>
    public Team(Camp camp, TeamType teamType)
    {
        TeamType = teamType;
        SurMemberList = [.. Enumerable.Range(0, 4).Select(_ => new Member(Enums.Camp.Sur))];
        HunMemberList.Add(new Member(Enums.Camp.Hun));

        GlobalBannedHunRecordList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select<int, Character?>(_ => null)];
        GlobalBannedSurRecordList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select<int, Character?>(_ => null)];

        GlobalBannedHunList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(_ => new Character(Enums.Camp.Hun))];
        GlobalBannedSurList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select(_ => new Character(Enums.Camp.Hun))];
        
        _surMemberOnFieldPrivateCollection = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        _surMemberOnFieldCollection = new ReadOnlyObservableCollection<Member?>(_surMemberOnFieldPrivateCollection);
        OnPropertyChanged(nameof(SurMemberOnFieldCollection));
        HunMemberOnField = null;

        Camp = camp;
    }

    /// <summary>
    /// Json的构造函数
    /// </summary>
    /// <param name="name">队伍名称</param>
    /// <param name="imageUri">队伍LOGO的Uri</param>
    /// <param name="surMemberList">求生者队员列表</param>
    /// <param name="hunMemberList">监管者队员列表</param>
    /// <param name="globalBannedHunList">全局被禁用的监管者列表(用于对局回溯)</param>
    /// <param name="globalBannedSurList">全局被禁用的求生者列表(用于对局回溯)</param>
    [JsonConstructor]
    internal Team(string name, string imageUri,
        ObservableCollection<Member>? surMemberList, ObservableCollection<Member>? hunMemberList,
        ObservableCollection<Character?>? globalBannedHunList = null,
        ObservableCollection<Character?>? globalBannedSurList = null)
    {
        Name = name;
        ImageUri = imageUri;
        SurMemberList = surMemberList ?? [.. Enumerable.Range(0, 4).Select(_ => new Member(Enums.Camp.Sur))];
        HunMemberList = hunMemberList ?? [new Member(Enums.Camp.Hun)];

        _surMemberOnFieldPrivateCollection = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        _surMemberOnFieldCollection = new ReadOnlyObservableCollection<Member?>(_surMemberOnFieldPrivateCollection);
        OnPropertyChanged(nameof(SurMemberOnFieldCollection));
        HunMemberOnField = null;

        GlobalBannedHunList = globalBannedHunList ??
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(_ => new Character(Enums.Camp.Hun))
        ];
        GlobalBannedSurList = globalBannedSurList ??
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select(_ => new Character(Enums.Camp.Hun))
        ];

        GlobalBannedHunRecordList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select<int, Character?>(_ => null)];
        GlobalBannedSurRecordList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select<int, Character?>(_ => null)];

        foreach (var m in SurMemberList.Where(m => m.IsOnField))
            MemberOnField(m);
        foreach (var m in HunMemberList.Where(m => m.IsOnField))
            MemberOnField(m);
    }

    /// <summary>
    /// 导入队伍信息，包括队伍名字、队标Uri、选手列表
    /// </summary>
    /// <param name="newTeam">队伍信息</param>
    public void ImportTeamInfo(Team newTeam)
    {
        Name = newTeam.Name;
        Logo = null;
        if (!string.IsNullOrEmpty(newTeam.ImageUri)&&newTeam.ImageUri!="null")
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

        GlobalBannedSurList = newTeam.GlobalBannedSurList;
        GlobalBannedHunList = newTeam.GlobalBannedHunList;

        _surMemberOnFieldPrivateCollection = [.. Enumerable.Range(0, 4).Select<int, Member?>(_ => null)];
        SurMemberOnFieldCollection = new ReadOnlyObservableCollection<Member?>(_surMemberOnFieldPrivateCollection);
        HunMemberOnField = null;

        _onFieldSurPlayerCnt = 0;

        foreach (var member in SurMemberList.Where(member => member.IsOnField))
        {
            MemberOnField(member);
        }

        foreach (var member in HunMemberList.Where(member => member.IsOnField))
        {
            MemberOnField(member);
        }

        MemberOnFieldChanged?.Invoke(this, EventArgs.Empty);
    }

    #region 选手操作

    /// <summary>
    /// 检查是否允许队员上场
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <returns>是否允许</returns>
    public bool CanMemberOnField(Camp camp)
    {
        if (camp == Enums.Camp.Hun)
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

        if (member.Camp == Enums.Camp.Hun)
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
        if (member.Camp == Enums.Camp.Hun)
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
                break;
            }
        }

        MemberOnFieldChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 选手上场状态改变
    /// </summary>
    public event EventHandler? MemberOnFieldChanged;

    #endregion

    /// <summary>
    /// 重置比分
    /// </summary>
    public void ResetScore() => Score.Reset();
}