using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示角色天赋配置的可观察对象模型
/// 包含两组独立控制的天赋开关状态：
/// 分别为求生者天赋与监管者天赋
/// </summary>
public partial class Talent : ObservableObject
{
    // 求生者天赋配置
    [ObservableProperty]
    private bool _borrowedTime = false;
    [ObservableProperty]
    private bool _flywheelEffect = false;
    [ObservableProperty]
    private bool _kneeJerkReflex = false;
    [ObservableProperty]
    private bool _tideTurner = false;

    // 监管者天赋配置
    [ObservableProperty]
    private bool _confinedSpace = false;
    [ObservableProperty]
    private bool _detention = false;
    [ObservableProperty]
    private bool _insolence = false;
    [ObservableProperty]
    private bool _trumpCard = false;
}