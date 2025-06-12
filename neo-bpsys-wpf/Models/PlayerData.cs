using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;
/// <summary>
/// 赛后数据类，用于存储赛后数据
/// </summary>
public partial class PlayerData : ObservableObject
{
    // Sur 相关数据
    [ObservableProperty]
    private string _machineDecoded = string.Empty; // 破译进度

    [ObservableProperty]
    private string _palletStunTimes = string.Empty; // 砸板命中次数

    [ObservableProperty]
    private string _rescueTimes = string.Empty; // 救人次数

    [ObservableProperty]
    private string _healedTimes = string.Empty; // 治疗次数

    [ObservableProperty]
    private string _kiteTime = string.Empty; // 牵制时间

    // Hun 相关数据
    [ObservableProperty]
    private string _machineLeft = string.Empty; // 剩余密码机数量

    [ObservableProperty]
    private string _palletBroken = string.Empty; // 破坏板子数

    [ObservableProperty]
    private string _hitTimes = string.Empty; // 命中求生者次数

    [ObservableProperty]
    private string _terrorShockTimes = string.Empty; // 恐惧震慑次数

    [ObservableProperty]
    private string _downTimes = string.Empty; // 击倒次数
}