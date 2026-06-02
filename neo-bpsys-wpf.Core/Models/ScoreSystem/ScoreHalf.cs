using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 中一个计分 Game 的半场结果。
/// </summary>
/// <remarks>
/// 半场会保存记录结果时的求生者 / 监管者对应主客队关系。这样导入对局、换边或后续改变当前阵营后，
/// 历史比分仍能归属到当时正确的主队或客队。
/// </remarks>
public partial class ScoreHalf : ObservableObjectBase
{
    private GameProgress _progress;
    private ScoreHalfKind _halfKind;
    private GameResult? _result;
    private TeamType? _surTeamTypeWhenRecorded;
    private TeamType? _hunTeamTypeWhenRecorded;

    /// <summary>
    /// 创建半场比分。
    /// </summary>
    /// <param name="progress">该半场对应的对局进度。</param>
    /// <param name="halfKind">第一半或第二半。</param>
    /// <param name="result">可空比赛结果；<see langword="null"/> 表示未记录或已清除。</param>
    /// <param name="surTeamTypeWhenRecorded">记录时求生者阵营对应的主客队。</param>
    /// <param name="hunTeamTypeWhenRecorded">记录时监管者阵营对应的主客队。</param>
    [JsonConstructor]
    public ScoreHalf(
        GameProgress progress,
        ScoreHalfKind halfKind,
        GameResult? result = null,
        TeamType? surTeamTypeWhenRecorded = null,
        TeamType? hunTeamTypeWhenRecorded = null)
    {
        _progress = progress;
        _halfKind = halfKind;
        _result = result;
        _surTeamTypeWhenRecorded = surTeamTypeWhenRecorded;
        _hunTeamTypeWhenRecorded = hunTeamTypeWhenRecorded;
    }

    /// <summary>
    /// 该半场对应的对局进度。
    /// </summary>
    public GameProgress Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    /// <summary>
    /// 该半场在所属计分 Game 中的位置。
    /// </summary>
    public ScoreHalfKind HalfKind
    {
        get => _halfKind;
        set => SetProperty(ref _halfKind, value);
    }

    /// <summary>
    /// 半场比赛结果；<see langword="null"/> 表示未记录或已清除。
    /// </summary>
    public GameResult? Result
    {
        get => _result;
        set => SetPropertyWithDerivedRefresh(ref _result, value);
    }

    /// <summary>
    /// 记录本半场时，求生者阵营对应的主客队。
    /// </summary>
    public TeamType? SurTeamTypeWhenRecorded
    {
        get => _surTeamTypeWhenRecorded;
        set => SetPropertyWithDerivedRefresh(ref _surTeamTypeWhenRecorded, value);
    }

    /// <summary>
    /// 记录本半场时，监管者阵营对应的主客队。
    /// </summary>
    public TeamType? HunTeamTypeWhenRecorded
    {
        get => _hunTeamTypeWhenRecorded;
        set => SetPropertyWithDerivedRefresh(ref _hunTeamTypeWhenRecorded, value);
    }

    /// <summary>
    /// 是否已有半场结果。
    /// </summary>
    [JsonIgnore] public bool HasResult => Result != null;

    /// <summary>
    /// 从 <see cref="Result"/> 派生的求生者小比分。
    /// </summary>
    [JsonIgnore]
    public int? SurMinorScore => Result switch
    {
        GameResult.Escape4 => 5,
        GameResult.Escape3 => 3,
        GameResult.Tie => 2,
        GameResult.Out3 => 1,
        GameResult.Out4 => 0,
        null => null,
        _ => null
    };

    /// <summary>
    /// 从 <see cref="Result"/> 派生的监管者小比分。
    /// </summary>
    [JsonIgnore]
    public int? HunMinorScore => Result switch
    {
        GameResult.Escape4 => 0,
        GameResult.Escape3 => 1,
        GameResult.Tie => 2,
        GameResult.Out3 => 3,
        GameResult.Out4 => 5,
        null => null,
        _ => null
    };

    /// <summary>
    /// 根据记录时阵营映射派生的主队小比分。
    /// </summary>
    [JsonIgnore]
    public int? HomeMinorScore
    {
        get
        {
            if (!SurMinorScore.HasValue || !HunMinorScore.HasValue)
                return null;

            return (SurTeamTypeWhenRecorded, HunTeamTypeWhenRecorded) switch
            {
                (TeamType.HomeTeam, TeamType.AwayTeam) => SurMinorScore,
                (TeamType.AwayTeam, TeamType.HomeTeam) => HunMinorScore,
                _ => null
            };
        }
    }

    /// <summary>
    /// 根据记录时阵营映射派生的客队小比分。
    /// </summary>
    [JsonIgnore]
    public int? AwayMinorScore
    {
        get
        {
            if (!SurMinorScore.HasValue || !HunMinorScore.HasValue)
                return null;

            return (SurTeamTypeWhenRecorded, HunTeamTypeWhenRecorded) switch
            {
                (TeamType.HomeTeam, TeamType.AwayTeam) => HunMinorScore,
                (TeamType.AwayTeam, TeamType.HomeTeam) => SurMinorScore,
                _ => null
            };
        }
    }

    /// <summary>
    /// 主队小比分显示文本；未记录时为 <c>-</c>。
    /// </summary>
    [JsonIgnore] public string HomeDisplayText => FormatDisplayText(HomeMinorScore);

    /// <summary>
    /// 客队小比分显示文本；未记录时为 <c>-</c>。
    /// </summary>
    [JsonIgnore] public string AwayDisplayText => FormatDisplayText(AwayMinorScore);

    private static string FormatDisplayText(int? score) =>
        score?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private bool SetPropertyWithDerivedRefresh<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value))
            return false;

        NotifyDerivedPropertiesChanged();
        return true;
    }

    private void NotifyDerivedPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(SurMinorScore));
        OnPropertyChanged(nameof(HunMinorScore));
        OnPropertyChanged(nameof(HomeMinorScore));
        OnPropertyChanged(nameof(AwayMinorScore));
        OnPropertyChanged(nameof(HomeDisplayText));
        OnPropertyChanged(nameof(AwayDisplayText));
    }
}
