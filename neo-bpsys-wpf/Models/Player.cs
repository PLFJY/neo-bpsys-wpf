using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示游戏中的选手模型
/// 实现属性通知功能以支持MVVM模式的数据绑定
/// </summary>
public partial class Player : ObservableObject
{
    /// <summary>
    /// 使用阵营和可选位置初始化选手对象
    /// </summary>
    /// <param name="camp">选手所属阵营（求生者/监管者）</param>
    /// <param name="position">选手在队伍中的位置索引（可空）</param>
    public Player(Camp camp, int? position = null)
    {
        this.Character = new Character(camp);
    }

    /// <summary>
    /// 使用阵营、名称和图像初始化选手对象
    /// </summary>
    /// <param name="camp">选手所属阵营（求生者/监管者）</param>
    /// <param name="name">选手角色名称</param>
    /// <param name="image">选手角色显示图像</param>
    public Player(Camp camp, string name, BitmapImage image)
    {
        this.Character = new Character(camp);
    }

    /// <summary>
    /// 当前选手关联的成员对象
    /// 包含基础属性和状态信息
    /// </summary>
    [ObservableProperty]
    private Member _member = new();

    /// <summary>
    /// 标识当前成员数据是否通过有效性校验
    /// 用于UI状态同步和操作控制
    /// </summary>
    [ObservableProperty]
    private bool _isMemberValid = false;

    /// <summary>
    /// 选手当前选择的角色对象
    /// </summary>
    [ObservableProperty]
    private Character? _character;

    /// <summary>
    /// 选手的天赋配置
    /// </summary>
    [ObservableProperty]
    private Talent _talent = new();

    /// <summary>
    /// 选手的辅助特质
    /// </summary>
    [ObservableProperty]
    private Trait _trait = new(null);

    /// <summary>
    /// 选手持久化数据容器
    /// 包含进度保存和加载所需的状态信息
    /// </summary>
    [ObservableProperty]
    private PlayerData? _data;
}