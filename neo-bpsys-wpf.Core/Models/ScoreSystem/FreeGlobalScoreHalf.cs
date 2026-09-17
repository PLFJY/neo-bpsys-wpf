using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// 自由对局 ScoreGlobal 中一个可完全手动编辑的半场。
/// </summary>
public sealed class FreeGlobalScoreHalf : ObservableObjectBase
{
    private bool _isCompleted;
    private int _homeMinorScore;
    private int _awayMinorScore;
    private Camp _homeCamp = Camp.Sur;
    private Camp _awayCamp = Camp.Hun;
    private bool _isInitialized;

    /// <summary>初始化自由全局比分半场。</summary>
    /// <param name="halfKind">半场位置。</param>
    public FreeGlobalScoreHalf(ScoreHalfKind halfKind)
    {
        HalfKind = halfKind;
    }

    /// <summary>获取半场位置。</summary>
    public ScoreHalfKind HalfKind { get; }

    /// <summary>获取或设置该半场是否完成。</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>获取或设置该半场是否已经完成过首次手动初始化。</summary>
    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value);
    }

    /// <summary>获取或设置主队小比分。</summary>
    public int HomeMinorScore
    {
        get => _homeMinorScore;
        set => SetProperty(ref _homeMinorScore, value);
    }

    /// <summary>获取或设置客队小比分。</summary>
    public int AwayMinorScore
    {
        get => _awayMinorScore;
        set => SetProperty(ref _awayMinorScore, value);
    }

    /// <summary>获取或设置主队阵营图标。</summary>
    public Camp HomeCamp
    {
        get => _homeCamp;
        set => SetProperty(ref _homeCamp, value);
    }

    /// <summary>获取或设置客队阵营图标。</summary>
    public Camp AwayCamp
    {
        get => _awayCamp;
        set => SetProperty(ref _awayCamp, value);
    }

    /// <summary>清除该半场及其保留值。</summary>
    public void Reset()
    {
        IsCompleted = false;
        IsInitialized = false;
        HomeMinorScore = 0;
        AwayMinorScore = 0;
        HomeCamp = Camp.Sur;
        AwayCamp = Camp.Hun;
    }

    /// <summary>创建当前半场的独立副本。</summary>
    /// <returns>独立副本。</returns>
    public FreeGlobalScoreHalf Clone() => new(HalfKind)
    {
        IsCompleted = IsCompleted,
        IsInitialized = IsInitialized,
        HomeMinorScore = HomeMinorScore,
        AwayMinorScore = AwayMinorScore,
        HomeCamp = HomeCamp,
        AwayCamp = AwayCamp
    };
}
