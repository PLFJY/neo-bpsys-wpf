namespace neo_bpsys_wpf.Models;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class PlayerData : ObservableObject
{
    // Sur 相关数据
    [ObservableProperty]
    private int _machineDecoded = 0; // 破译进度

    [ObservableProperty]
    private int _palletStunTimes = 0; // 砸板命中次数

    [ObservableProperty]
    private int _rescueTimes = 0; // 救人次数

    [ObservableProperty]
    private int _healedTimes = 0; // 治疗次数

    [ObservableProperty]
    private int _kiteTime = 0; // 牵制时间

    // Hun 相关数据
    [ObservableProperty]
    private int _machineLeft = 0; // 剩余密码机数量

    [ObservableProperty]
    private int _palletBroken = 0; // 破坏板子数

    [ObservableProperty]
    private int _hitTimes = 0; // 命中求生者次数

    [ObservableProperty]
    private int _terrorShockTimes = 0; // 恐惧震慑次数

    [ObservableProperty]
    private int _downTimes = 0; // 击倒次数
}