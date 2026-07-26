using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

namespace neo_bpsys_wpf.Core.Models;

[FrontedBindingObject]
public partial class Talent : ObservableObjectBase
{
    #region Sur

    /// <summary>
    /// 回光返照
    /// </summary>
    [ObservableProperty]
    public partial bool BorrowedTime { get; set; }

    /// <summary>
    /// 飞轮效应
    /// </summary>
    [ObservableProperty]
    public partial bool FlywheelEffect { get; set; }

    /// <summary>
    /// 膝跳反射
    /// </summary>
    [ObservableProperty]
    public partial bool KneeJerkReflex { get; set; }

    /// <summary>
    /// 化险为夷
    /// </summary>
    [ObservableProperty]
    public partial bool TideTurner { get; set; }

    #endregion

    #region Hun

    /// <summary>
    /// 禁闭空间
    /// </summary>
    [ObservableProperty]
    public partial bool ConfinedSpace { get; set; }

    /// <summary>
    /// 挽留
    /// </summary>
    [ObservableProperty]
    public partial bool Detention { get; set; }

    /// <summary>
    /// 张狂
    /// </summary>
    [ObservableProperty]
    public partial bool Insolence { get; set; }

    /// <summary>
    /// 底牌
    /// </summary>
    [ObservableProperty]
    public partial bool TrumpCard { get; set; }

    #endregion
}
