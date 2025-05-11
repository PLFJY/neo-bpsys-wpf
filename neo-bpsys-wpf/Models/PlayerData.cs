namespace neo_bpsys_wpf.Models;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// 表示玩家数据的模型类，继承自ObservableObject以支持MVVM模式下的属性通知
/// 包含求生者和监管者的特定数据字段
/// </summary>
public partial class PlayerData : ObservableObject
{
    // Sur 相关数据（求生者数据）
    
    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置破译进度（机器解码数）
    /// </summary>
    [ObservableProperty]
    private int _machineDecoded = 0; // 破译进度

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置砸板命中次数
    /// </summary>
    [ObservableProperty]
    private int _palletStunTimes = 0; // 砸板命中次数

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置救人次数
    /// </summary>
    [ObservableProperty]
    private int _rescueTimes = 0; // 救人次数

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置治疗次数
    /// </summary>
    [ObservableProperty]
    private int _healedTimes = 0; // 治疗次数

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置牵制时间
    /// </summary>
    [ObservableProperty]
    private int _kiteTime = 0; // 牵制时间

    // Hun 相关数据（监管者数据）
    
    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置剩余密码机数量
    /// </summary>
    [ObservableProperty]
    private int _machineLeft = 0; // 剩余密码机数量

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置破坏板子数
    /// </summary>
    [ObservableProperty]
    private int _palletBroken = 0; // 破坏板子数

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置命中求生者次数
    /// </summary>
    [ObservableProperty]
    private int _hitTimes = 0; // 命中求生者次数

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置恐惧震慑次数
    /// </summary>
    [ObservableProperty]
    private int _terrorShockTimes = 0; // 恐惧震慑次数

    /// <summary>
    /// [ObservableProperty]表示该属性是可观察的
    /// 获取或设置击倒次数
    /// </summary>
    [ObservableProperty]
    private int _downTimes = 0; // 击倒次数
}