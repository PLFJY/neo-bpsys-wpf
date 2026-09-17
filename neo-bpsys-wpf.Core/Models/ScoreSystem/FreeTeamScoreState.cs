using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// 自由对局中一支主客队的可手动编辑比分。
/// </summary>
public sealed class FreeTeamScoreState : ObservableObjectBase
{
    private int _currentMinorScore;
    private int _totalMinorScore;
    private int _majorWin;
    private int _majorTie;

    /// <summary>获取或设置当前大场内的小比分。</summary>
    public int CurrentMinorScore
    {
        get => _currentMinorScore;
        set => SetProperty(ref _currentMinorScore, value);
    }

    /// <summary>获取或设置自由对局总小比分。</summary>
    public int TotalMinorScore
    {
        get => _totalMinorScore;
        set => SetProperty(ref _totalMinorScore, value);
    }

    /// <summary>获取或设置大场胜场数。</summary>
    public int MajorWin
    {
        get => _majorWin;
        set => SetProperty(ref _majorWin, value);
    }

    /// <summary>获取或设置大场平局数。</summary>
    public int MajorTie
    {
        get => _majorTie;
        set => SetProperty(ref _majorTie, value);
    }

    /// <summary>将全部自由比分字段重置为零。</summary>
    public void Reset()
    {
        CurrentMinorScore = 0;
        TotalMinorScore = 0;
        MajorWin = 0;
        MajorTie = 0;
    }

    /// <summary>创建当前状态的独立副本。</summary>
    /// <returns>独立的自由队伍比分副本。</returns>
    public FreeTeamScoreState Clone() => new()
    {
        CurrentMinorScore = CurrentMinorScore,
        TotalMinorScore = TotalMinorScore,
        MajorWin = MajorWin,
        MajorTie = MajorTie
    };
}
