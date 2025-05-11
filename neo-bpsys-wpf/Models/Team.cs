using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示游戏中的队伍模型，包含阵营标识、成员列表、禁用角色、场上玩家等核心数据
/// 实现INotifyPropertyChanged接口以支持属性变更通知
/// </summary>
public partial class Team : ObservableObject
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    /// <summary>
    /// 设计时构造函数（用于XAML设计器实例化）
    /// </summary>
    public Team()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private string _name = string.Empty;

    /// <summary>
    /// 获取或设置队伍名称（重写ToString方法实现动态名称显示）
    /// </summary>
    public string Name
    {
        get
        {
            return this.ToString();
        }
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }


    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    private ImageSource? _logo;

    /// <summary>
    /// 图标资源的URI路径（用于Logo属性的序列化存储）
    /// </summary>
    public string ImageUri { get; set; } = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Member> _surMemberList = new();

    [ObservableProperty]
    private ObservableCollection<Member> _hunMemberList = new();

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedSurList = new();

    [ObservableProperty]
    private ObservableCollection<Character?> _globalBannedHunList = new();

    /// <summary>
    /// 全局禁用的求生者角色记录数组（固定容量9个）
    /// </summary>
    public Character?[] GlobalBannedSurRecordArray { get; set; } = new Character[9];

    /// <summary>
    /// 全局禁用的监管者角色记录数组（固定容量3个）
    /// </summary>
    public Character?[] GlobalBannedHunRecordArray { get; set; } = new Character[3];

    [ObservableProperty]
    [JsonIgnore]
    private ObservableCollection<Player> _surPlayerOnFieldList = new();

    [ObservableProperty]
    [JsonIgnore]
    private Player _hunPlayerOnField;

    [ObservableProperty]
    private Score _score = new();

    /// <summary>
    /// 初始化新队伍（创建默认成员和玩家配置）
    /// </summary>
    /// <param name="camp">阵营（目前写注释的人没有发现它的作用）</param>
    public Team(Camp camp)
    {
        SurMemberList.AddRange(Enumerable.Range(0, 4).Select(i => new Member(Camp.Sur)));
        HunMemberList.Add(new Member(Camp.Hun));

        Camp = camp;

        GlobalBannedHunList.AddRange(Enumerable.Range(0, 3).Select(i => new Character(Camp.Hun)));
        GlobalBannedSurList.AddRange(Enumerable.Range(0, 9).Select(i => new Character(Camp.Sur)));

        SurPlayerOnFieldList.AddRange(Enumerable.Range(0, 4).Select(i => new Player(Camp.Sur, i)));
        HunPlayerOnField = new Player(Camp.Hun);
    }

    /// <summary>
    /// 从另一个Team对象导入基础信息（名称、图标、成员列表）
    /// </summary>
    /// <param name="newTeam">包含新数据的源Team对象</param>
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
        SurPlayerOnFieldList.AddRange(Enumerable.Range(0, 4).Select(i => new Player(Camp.Sur, i)));
        HunPlayerOnField = new Player(Camp.Hun);

        OnPropertyChanged();
    }

    /// <summary>
    /// 检查指定阵营是否可以添加新成员到场上
    /// </summary>
    /// <param name="camp">要检查的阵营（SUR/HUN）</param>
    /// <returns>当存在可用位置时返回true</returns>
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
    /// 将指定成员添加到对应阵营的场上位置
    /// </summary>
    /// <param name="member">要添加的成员对象</param>
    /// <returns>成功添加返回true，否则false</returns>
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
    /// 指定成员下场
    /// </summary>
    /// <param name="member">要下场的成员对象</param>
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

    /// <summary>
    /// 同步全局禁用记录数组到可观察集合（用于UI绑定更新）
    /// </summary>
    public void SyncGlobalBanWithRecord()
    {
        GlobalBannedSurList = new(GlobalBannedSurRecordArray.ToList());
        GlobalBannedHunList = new(GlobalBannedHunRecordArray.ToList());
    }

    /// <summary>
    /// 返回队伍显示名称（主队/客队特殊处理）
    /// </summary>
    /// <returns>队伍名称字符串</returns>
    public override string ToString()
    {
        if (this == App.Services.GetRequiredService<ISharedDataService>().MainTeam)
        {
            return string.IsNullOrEmpty(_name) ? "主队" : _name;
        }
        if (this == App.Services.GetRequiredService<ISharedDataService>().AwayTeam)
        {
            return string.IsNullOrEmpty(_name) ? "客队" : _name;
        }
        return _name;
    }
}