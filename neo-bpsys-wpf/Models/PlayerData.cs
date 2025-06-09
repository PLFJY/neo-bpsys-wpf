using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;
/// <summary>
/// 赛后数据类，用于存储赛后数据
/// </summary>
public partial class PlayerData : ObservableObject
{
    // Sur 相关数据
    [ObservableProperty]
    private double _machineDecoded = 0; // 破译进度

    [ObservableProperty]
    private double _palletStunTimes = 0; // 砸板命中次数

    [ObservableProperty]
    private double _rescueTimes = 0; // 救人次数

    [ObservableProperty]
    private double _healedTimes = 0; // 治疗次数

    [ObservableProperty]
    private double _kiteTime = 0; // 牵制时间

    // Hun 相关数据
    [ObservableProperty]
    private double _machineLeft = 0; // 剩余密码机数量

    [ObservableProperty]
    private double _palletBroken = 0; // 破坏板子数

    [ObservableProperty]
    private double _hitTimes = 0; // 命中求生者次数

    [ObservableProperty]
    private double _terrorShockTimes = 0; // 恐惧震慑次数

    [ObservableProperty]
    private double _downTimes = 0; // 击倒次数
}