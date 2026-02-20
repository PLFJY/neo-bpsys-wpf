using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.Core.Models;
/// <summary>
/// 赛后数据类，用于存储赛后数据
/// </summary>
public partial class PlayerData : ObservableObjectBase
{
    #region Sur

    private string _decodingProgress = string.Empty;

    /// <summary>
    /// 破译进度
    /// </summary>
    public string DecodingProgress
    {
        get => string.IsNullOrWhiteSpace(_decodingProgress) ? "-" : $"{_decodingProgress}%";
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().TrimEnd('%').Trim();
            SetProperty(ref _decodingProgress, normalized);
        }
    }

    private string _palletStrikes = string.Empty;

    /// <summary>
    /// 砸板命中次数
    /// </summary>
    public string PalletStrikes
    {
        get => string.IsNullOrWhiteSpace(_palletStrikes) ? "-" : _palletStrikes;
        set => SetProperty(ref _palletStrikes, value);
    }

    private string _rescues = string.Empty;

    /// <summary>
    /// 救人次数
    /// </summary>
    public string Rescues
    {
        get => string.IsNullOrWhiteSpace(_rescues) ? "-" : _rescues;
        set => SetProperty(ref _rescues, value);
    }

    private string _heals = string.Empty;

    /// <summary>
    /// 治疗次数
    /// </summary>
    public string Heals
    {
        get => string.IsNullOrWhiteSpace(_heals) ? "-" : _heals;
        set => SetProperty(ref _heals, value);
    }

    private string _containmentTime = string.Empty;

    /// <summary>
    /// 牵制时间
    /// </summary>
    public string ContainmentTime
    {
        get => string.IsNullOrWhiteSpace(_containmentTime) ? "-" : _containmentTime;
        set => SetProperty(ref _containmentTime, value);
    }

    #endregion
    #region Hun
    private string _remainingCipher = string.Empty;

    /// <summary>
    /// 剩余密码机数量
    /// </summary>
    public string RemainingCipher
    {
        get => string.IsNullOrWhiteSpace(_remainingCipher) ? "-" : _remainingCipher;
        set => SetProperty(ref _remainingCipher, value);
    }

    private string _palletsDestroyed = string.Empty;

    /// <summary>
    /// 破坏板子数
    /// </summary>
    public string PalletsDestroyed
    {
        get => string.IsNullOrWhiteSpace(_palletsDestroyed) ? "-" : _palletsDestroyed;
        set => SetProperty(ref _palletsDestroyed, value);
    }

    private string _survivorHits = string.Empty;

    /// <summary>
    /// 命中求生者次数
    /// </summary>
    public string SurvivorHits
    {
        get => string.IsNullOrWhiteSpace(_survivorHits) ? "-" : _survivorHits;
        set => SetProperty(ref _survivorHits, value);
    }

    private string _terrorShocks = string.Empty;

    /// <summary>
    /// 恐惧震慑次数
    /// </summary>
    public string TerrorShocks
    {
        get => string.IsNullOrWhiteSpace(_terrorShocks) ? "-" : _terrorShocks;
        set => SetProperty(ref _terrorShocks, value);
    }

    private string _knockdowns = string.Empty;

    /// <summary>
    /// 击倒次数
    /// </summary>
    public string Knockdowns
    {
        get => string.IsNullOrWhiteSpace(_knockdowns) ? "-" : _knockdowns;
        set => SetProperty(ref _knockdowns, value);
    }

    #endregion
}
