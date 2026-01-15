using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.Core.Models;
/// <summary>
/// 赛后数据类，用于存储赛后数据
/// </summary>
public partial class PlayerData : ObservableObjectBase
{
    #region Sur

    /// <summary>
    /// 破译进度
    /// </summary>
    [ObservableProperty]
    private string _decodingProgress = string.Empty; // 破译进度

    /// <summary>
    /// 砸板命中次数
    /// </summary>
    [ObservableProperty]
    private string _palletStrikes = string.Empty; // 砸板命中次数

    /// <summary>
    /// 救人次数
    /// </summary>
    [ObservableProperty]
    private string _rescues = string.Empty; // 救人次数

    /// <summary>
    /// 治疗次数
    /// </summary>
    [ObservableProperty]
    private string _heals = string.Empty; // 治疗次数

    /// <summary>
    /// 牵制时间
    /// </summary>
    [ObservableProperty]
    private string _containmentTime = string.Empty; // 牵制时间

    #endregion
    #region Hun
    /// <summary>
    /// 剩余密码机数量
    /// </summary>
    [ObservableProperty]
    private string _remainingCipher = string.Empty; // 剩余密码机数量

    /// <summary>
    /// 破坏板子数
    /// </summary>
    [ObservableProperty]
    private string _palletsDestroyed = string.Empty; // 破坏板子数

    /// <summary>
    /// 命中求生者次数
    /// </summary>
    [ObservableProperty]
    private string _survivorHits = string.Empty; // 命中求生者次数

    /// <summary>
    /// 恐惧震慑次数
    /// </summary>
    [ObservableProperty]
    private string _terrorShocks = string.Empty; // 恐惧震慑次数

    /// <summary>
    /// 击倒次数
    /// </summary>
    [ObservableProperty]
    private string _knockdowns = string.Empty; // 击倒次数

    #endregion
}