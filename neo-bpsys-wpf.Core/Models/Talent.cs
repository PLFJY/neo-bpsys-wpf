using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.Core.Models;

public partial class Talent : ObservableObjectBase
{
    #region Sur

    /// <summary>
    /// 回光返照
    /// </summary>
    [ObservableProperty] private bool _borrowedTime;

    /// <summary>
    /// 飞轮效应
    /// </summary>
    [ObservableProperty] private bool _flywheelEffect;

    /// <summary>
    /// 膝跳反射
    /// </summary>
    [ObservableProperty] private bool _kneeJerkReflex;

    /// <summary>
    /// 化险为夷
    /// </summary>
    [ObservableProperty] private bool _tideTurner;

    #endregion

    #region Hun

    /// <summary>
    /// 禁闭空间
    /// </summary>
    [ObservableProperty] private bool _confinedSpace;

    /// <summary>
    /// 挽留
    /// </summary>
    [ObservableProperty] private bool _detention;

    /// <summary>
    /// 张狂
    /// </summary>
    [ObservableProperty] private bool _insolence;

    /// <summary>
    /// 底牌
    /// </summary>
    [ObservableProperty] private bool _trumpCard;

    #endregion
}