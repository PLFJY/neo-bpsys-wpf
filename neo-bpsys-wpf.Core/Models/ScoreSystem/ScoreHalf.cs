using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

public partial class ScoreHalf : ObservableObjectBase
{
    private GameProgress _progress;
    private ScoreHalfKind _halfKind;
    private GameResult? _result;
    private TeamType? _surTeamTypeWhenRecorded;
    private TeamType? _hunTeamTypeWhenRecorded;

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

    public GameProgress Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public ScoreHalfKind HalfKind
    {
        get => _halfKind;
        set => SetProperty(ref _halfKind, value);
    }

    public GameResult? Result
    {
        get => _result;
        set => SetPropertyWithDerivedRefresh(ref _result, value);
    }

    public TeamType? SurTeamTypeWhenRecorded
    {
        get => _surTeamTypeWhenRecorded;
        set => SetPropertyWithDerivedRefresh(ref _surTeamTypeWhenRecorded, value);
    }

    public TeamType? HunTeamTypeWhenRecorded
    {
        get => _hunTeamTypeWhenRecorded;
        set => SetPropertyWithDerivedRefresh(ref _hunTeamTypeWhenRecorded, value);
    }

    [JsonIgnore] public bool HasResult => Result != null;

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

    [JsonIgnore] public string HomeDisplayText => FormatDisplayText(HomeMinorScore);

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
