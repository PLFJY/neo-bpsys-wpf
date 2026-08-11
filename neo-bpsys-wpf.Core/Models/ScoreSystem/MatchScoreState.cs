using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// Score System v2 的权威整场比分状态，由 <see cref="Game.MatchScore"/> 持有并随对局导入导出。
/// </summary>
/// <remarks>
/// 此类型保存可序列化的半场结果与记录时阵营映射，并派生大比分、总小比分和当前前台显示文本。
/// <see cref="Score"/> / <c>Team.Score</c> 仅用于旧数据反序列化，不再由运行时比分状态同步。
/// </remarks>
[FrontedBindingObject]
public partial class MatchScoreState : ObservableObjectBase
{
    private ObservableCollection<ScoreGame> _games;
    private int _homeMajorWin;
    private int _homeMajorTie;
    private int _awayMajorWin;
    private int _awayMajorTie;
    private string _homeMajorText = "W0  D0";
    private string _awayMajorText = "W0  D0";
    private int _homeTotalMinorScore;
    private int _awayTotalMinorScore;
    private int _currentSurTeamTotalMinorScore;
    private int _currentHunTeamTotalMinorScore;
    private string _currentSurTeamMinorGameScoreText = "0";
    private string _currentHunTeamMinorGameScoreText = "0";
    private string _currentSurTeamMinorHalfScoreText = "-";
    private string _currentHunTeamMinorHalfScoreText = "-";
    private string _currentSurTeamMajorText = "W0  D0";
    private string _currentHunTeamMajorText = "W0  D0";
    private int _currentSurTeamMajorWin;
    private int _currentSurTeamMajorTie;
    private int _currentHunTeamMajorWin;
    private int _currentHunTeamMajorTie;
    private GameProgress _currentDisplayProgress = GameProgress.Free;
    private TeamType _currentDisplaySurTeamType = TeamType.HomeTeam;
    private TeamType _currentDisplayHunTeamType = TeamType.AwayTeam;
    private bool _currentDisplayIsBo3Mode;
    private bool _lastRecalculateIsBo3Mode;
    private readonly MatchScoreHalfByProgressView _halfByProgress;

    /// <summary>
    /// 创建比分状态。未提供 <paramref name="games"/> 时会创建 BO3/BO5 支持的默认比分单元。
    /// </summary>
    /// <param name="games">可序列化的比分单元集合。</param>
    [JsonConstructor]
    public MatchScoreState(ObservableCollection<ScoreGame>? games = null)
    {
        _games = games ?? CreateDefaultGames();
        _halfByProgress = new MatchScoreHalfByProgressView(this);
        SubscribeGames(_games);
        Recalculate(isBo3Mode: false);
    }

    /// <summary>
    /// 按 <see cref="GameProgress"/> 和当前 BO 上下文取得单半场比分的只读索引入口。
    /// </summary>
    /// <remarks>
    /// 在 v3 BindingPath 中使用
    /// <c>CurrentGame.MatchScore.HalfByProgress[CurrentGame.GameProgress].SurMinorScore</c>。
    /// </remarks>
    [JsonIgnore]
    [FrontedBindable(IncludeChildren = false)]
    public MatchScoreHalfByProgressView HalfByProgress => _halfByProgress;

    internal bool LastRecalculateIsBo3Mode => _lastRecalculateIsBo3Mode;

    /// <summary>
    /// 可序列化的比分单元集合，包括普通局和加赛局。
    /// </summary>
    [FrontedBindingIgnore]
    public ObservableCollection<ScoreGame> Games
    {
        get => _games;
        set
        {
            if (_games == value)
                return;

            UnsubscribeGames(_games);
            SetProperty(ref _games, value);
            SubscribeGames(_games);
            Recalculate(_lastRecalculateIsBo3Mode);
        }
    }

    /// <summary>
    /// 主队大比分胜场数。
    /// </summary>
    [JsonIgnore]
    public int HomeMajorWin
    {
        get => _homeMajorWin;
        private set => SetProperty(ref _homeMajorWin, value);
    }

    /// <summary>
    /// 主队大比分平局数。
    /// </summary>
    [JsonIgnore]
    public int HomeMajorTie
    {
        get => _homeMajorTie;
        private set => SetProperty(ref _homeMajorTie, value);
    }

    /// <summary>
    /// 客队大比分胜场数。
    /// </summary>
    [JsonIgnore]
    public int AwayMajorWin
    {
        get => _awayMajorWin;
        private set => SetProperty(ref _awayMajorWin, value);
    }

    /// <summary>
    /// 客队大比分平局数。
    /// </summary>
    [JsonIgnore]
    public int AwayMajorTie
    {
        get => _awayMajorTie;
        private set => SetProperty(ref _awayMajorTie, value);
    }

    /// <summary>
    /// 主队前台大比分文本，例如 <c>W1  D0</c>。
    /// </summary>
    [JsonIgnore]
    public string HomeMajorText
    {
        get => _homeMajorText;
        private set => SetProperty(ref _homeMajorText, value);
    }

    /// <summary>
    /// 客队前台大比分文本，例如 <c>W1  D0</c>。
    /// </summary>
    [JsonIgnore]
    public string AwayMajorText
    {
        get => _awayMajorText;
        private set => SetProperty(ref _awayMajorText, value);
    }

    /// <summary>
    /// 主队已记录半场的小比分总和。
    /// </summary>
    [JsonIgnore]
    public int HomeTotalMinorScore
    {
        get => _homeTotalMinorScore;
        private set => SetProperty(ref _homeTotalMinorScore, value);
    }

    /// <summary>
    /// 客队已记录半场的小比分总和。
    /// </summary>
    [JsonIgnore]
    public int AwayTotalMinorScore
    {
        get => _awayTotalMinorScore;
        private set => SetProperty(ref _awayTotalMinorScore, value);
    }

    /// <summary>
    /// 当前求生者队伍在所有已记录半场中的小比分总和。
    /// </summary>
    [JsonIgnore]
    public int CurrentSurTeamTotalMinorScore
    {
        get => _currentSurTeamTotalMinorScore;
        private set => SetProperty(ref _currentSurTeamTotalMinorScore, value);
    }

    /// <summary>
    /// 当前监管者队伍在所有已记录半场中的小比分总和。
    /// </summary>
    [JsonIgnore]
    public int CurrentHunTeamTotalMinorScore
    {
        get => _currentHunTeamTotalMinorScore;
        private set => SetProperty(ref _currentHunTeamTotalMinorScore, value);
    }

    /// <summary>
    /// 当前 Game，从第一半累计到当前半场的求生者队伍小比分文本。
    /// </summary>
    [JsonIgnore]
    public string CurrentSurTeamMinorGameScoreText
    {
        get => _currentSurTeamMinorGameScoreText;
        private set
        {
            if (SetProperty(ref _currentSurTeamMinorGameScoreText, value))
                OnPropertyChanged(nameof(CurrentSurTeamMinorScoreText));
        }
    }

    /// <summary>
    /// 兼容字段：当前 Game，从第一半累计到当前半场的求生者队伍小比分文本。
    /// </summary>
    [JsonIgnore]
    public string CurrentSurTeamMinorScoreText => CurrentSurTeamMinorGameScoreText;

    /// <summary>
    /// 当前 Game，从第一半累计到当前半场的监管者队伍小比分文本。
    /// </summary>
    [JsonIgnore]
    public string CurrentHunTeamMinorGameScoreText
    {
        get => _currentHunTeamMinorGameScoreText;
        private set
        {
            if (SetProperty(ref _currentHunTeamMinorGameScoreText, value))
                OnPropertyChanged(nameof(CurrentHunTeamMinorScoreText));
        }
    }

    /// <summary>
    /// 兼容字段：当前 Game，从第一半累计到当前半场的监管者队伍小比分文本。
    /// </summary>
    [JsonIgnore]
    public string CurrentHunTeamMinorScoreText => CurrentHunTeamMinorGameScoreText;

    /// <summary>
    /// 当前单独半场的求生者队伍小比分文本；半场未记录时为 <c>-</c>。
    /// </summary>
    [JsonIgnore]
    public string CurrentSurTeamMinorHalfScoreText
    {
        get => _currentSurTeamMinorHalfScoreText;
        private set => SetProperty(ref _currentSurTeamMinorHalfScoreText, value);
    }

    /// <summary>
    /// 当前单独半场的监管者队伍小比分文本；半场未记录时为 <c>-</c>。
    /// </summary>
    [JsonIgnore]
    public string CurrentHunTeamMinorHalfScoreText
    {
        get => _currentHunTeamMinorHalfScoreText;
        private set => SetProperty(ref _currentHunTeamMinorHalfScoreText, value);
    }

    /// <summary>
    /// 当前对局进度对应的 ScoreGame。
    /// </summary>
    [JsonIgnore]
    [FrontedBindable]
    public ScoreGame? CurrentGameScore => GetGame(_currentDisplayProgress, _currentDisplayIsBo3Mode);

    /// <summary>
    /// 当前对局进度对应的 ScoreHalf。
    /// </summary>
    [JsonIgnore]
    [FrontedBindable]
    public ScoreHalf? CurrentHalf => GetHalf(_currentDisplayProgress, _currentDisplayIsBo3Mode);

    /// <summary>
    /// 当前求生者队伍对应的大比分文本。
    /// </summary>
    [JsonIgnore]
    public string CurrentSurTeamMajorText
    {
        get => _currentSurTeamMajorText;
        private set => SetProperty(ref _currentSurTeamMajorText, value);
    }

    /// <summary>
    /// 当前监管者队伍对应的大比分文本。
    /// </summary>
    [JsonIgnore]
    public string CurrentHunTeamMajorText
    {
        get => _currentHunTeamMajorText;
        private set => SetProperty(ref _currentHunTeamMajorText, value);
    }

    /// <summary>
    /// 当前求生者队伍的大比分胜场数原始数据。
    /// </summary>
    [JsonIgnore]
    public int CurrentSurTeamMajorWin
    {
        get => _currentSurTeamMajorWin;
        private set => SetProperty(ref _currentSurTeamMajorWin, value);
    }

    /// <summary>
    /// 当前求生者队伍的大比分平局数原始数据。
    /// </summary>
    [JsonIgnore]
    public int CurrentSurTeamMajorTie
    {
        get => _currentSurTeamMajorTie;
        private set => SetProperty(ref _currentSurTeamMajorTie, value);
    }

    /// <summary>
    /// 当前监管者队伍的大比分胜场数原始数据。
    /// </summary>
    [JsonIgnore]
    public int CurrentHunTeamMajorWin
    {
        get => _currentHunTeamMajorWin;
        private set => SetProperty(ref _currentHunTeamMajorWin, value);
    }

    /// <summary>
    /// 当前监管者队伍的大比分平局数原始数据。
    /// </summary>
    [JsonIgnore]
    public int CurrentHunTeamMajorTie
    {
        get => _currentHunTeamMajorTie;
        private set => SetProperty(ref _currentHunTeamMajorTie, value);
    }

    /// <summary>
    /// 创建包含所有受支持比分单元的默认空比分状态。
    /// </summary>
    public static MatchScoreState CreateDefault() => new(CreateDefaultGames());

    /// <summary>
    /// 创建独立可变副本，避免新旧 <see cref="Game"/> 共享同一比分实例。
    /// </summary>
    public MatchScoreState Clone()
    {
        var games = new ObservableCollection<ScoreGame>(
            Games.Select(game => new ScoreGame(
                game.Key,
                CloneHalf(game.FirstHalf),
                CloneHalf(game.SecondHalf))));

        var clone = new MatchScoreState(games);
        clone._currentDisplayProgress = _currentDisplayProgress;
        clone._currentDisplaySurTeamType = _currentDisplaySurTeamType;
        clone._currentDisplayHunTeamType = _currentDisplayHunTeamType;
        clone._currentDisplayIsBo3Mode = _currentDisplayIsBo3Mode;
        clone._lastRecalculateIsBo3Mode = _lastRecalculateIsBo3Mode;
        clone.Recalculate(_lastRecalculateIsBo3Mode);
        clone.UpdateCurrentDisplay();
        return clone;
    }

    /// <summary>
    /// 根据对局进度解析比分单元。缺少 BO3/BO5 上下文时，重叠 enum 值按 BO5 第四局处理。
    /// </summary>
    public ScoreGame? GetGame(GameProgress progress) => GetGame(progress, isBo3Mode: false);

    /// <summary>
    /// 根据对局进度和 BO3/BO5 上下文解析比分单元。
    /// </summary>
    /// <param name="progress">当前对局进度。</param>
    /// <param name="isBo3Mode">是否按 BO3 解析 Game3 overtime 和 Game4 重叠值。</param>
    public ScoreGame? GetGame(GameProgress progress, bool isBo3Mode)
    {
        var key = ResolveScoreGameKey(progress, isBo3Mode);
        return key == null ? null : Games.FirstOrDefault(game => game.Key == key.Value);
    }

    /// <summary>
    /// 根据对局进度解析半场。缺少 BO3/BO5 上下文时，重叠 enum 值按 BO5 第四局处理。
    /// </summary>
    public ScoreHalf? GetHalf(GameProgress progress) => GetHalf(progress, isBo3Mode: false);

    /// <summary>
    /// 根据对局进度和 BO3/BO5 上下文解析半场。
    /// </summary>
    /// <param name="progress">当前对局进度。</param>
    /// <param name="isBo3Mode">是否按 BO3 解析 Game3 overtime 和 Game4 重叠值。</param>
    public ScoreHalf? GetHalf(GameProgress progress, bool isBo3Mode)
    {
        var game = GetGame(progress, isBo3Mode);
        if (game == null)
            return null;

        var halfKind = ResolveHalfKind(progress);
        return halfKind switch
        {
            ScoreHalfKind.FirstHalf => game.FirstHalf,
            ScoreHalfKind.SecondHalf => game.SecondHalf,
            _ => null
        };
    }

    /// <summary>
    /// 按指定 BO 模式从所有可见且已记录的比分单元重新派生大比分、总小比分和当前显示文本。
    /// </summary>
    /// <param name="isBo3Mode">是否按 BO3 可见性计算；否则按 BO5 可见性计算。</param>
    public void Recalculate(bool isBo3Mode)
    {
        _lastRecalculateIsBo3Mode = isBo3Mode;
        OnPropertyChanged(nameof(HalfByProgress));

        var homeMajorWin = 0;
        var homeMajorTie = 0;
        var awayMajorWin = 0;
        var awayMajorTie = 0;
        var homeTotalMinorScore = 0;
        var awayTotalMinorScore = 0;

        foreach (var game in Games.Where(game => ScoreGameVisibility.IsVisibleInBoMode(game.Key, isBo3Mode)))
        {
            switch (game.MajorResult)
            {
                case ScoreGameMajorResult.HomeWin:
                    homeMajorWin++;
                    break;
                case ScoreGameMajorResult.AwayWin:
                    awayMajorWin++;
                    break;
                case ScoreGameMajorResult.Tie:
                    homeMajorTie++;
                    awayMajorTie++;
                    break;
            }

            foreach (var half in game.Halves)
            {
                homeTotalMinorScore += half.HomeMinorScore ?? 0;
                awayTotalMinorScore += half.AwayMinorScore ?? 0;
            }
        }

        HomeMajorWin = homeMajorWin;
        HomeMajorTie = homeMajorTie;
        AwayMajorWin = awayMajorWin;
        AwayMajorTie = awayMajorTie;
        HomeMajorText = FormatMajorText(HomeMajorWin, HomeMajorTie);
        AwayMajorText = FormatMajorText(AwayMajorWin, AwayMajorTie);
        HomeTotalMinorScore = homeTotalMinorScore;
        AwayTotalMinorScore = awayTotalMinorScore;
        UpdateCurrentDisplay();
    }

    /// <summary>
    /// 刷新当前局内比分显示。缺少 BO3/BO5 上下文时，重叠 enum 值按 BO5 第四局处理。
    /// </summary>
    public void RefreshCurrentDisplay(GameProgress progress, TeamType currentSurTeamType, TeamType currentHunTeamType)
    {
        RefreshCurrentDisplay(progress, currentSurTeamType, currentHunTeamType, isBo3Mode: false);
    }

    /// <summary>
    /// 结合当前进度、当前阵营映射和 BO3/BO5 上下文刷新局内比分显示。
    /// </summary>
    public void RefreshCurrentDisplay(
        GameProgress progress,
        TeamType currentSurTeamType,
        TeamType currentHunTeamType,
        bool isBo3Mode)
    {
        _currentDisplayProgress = progress;
        _currentDisplaySurTeamType = currentSurTeamType;
        _currentDisplayHunTeamType = currentHunTeamType;
        _currentDisplayIsBo3Mode = isBo3Mode;
        UpdateCurrentDisplay();
    }

    private void UpdateCurrentDisplay()
    {
        CurrentSurTeamMajorText = GetMajorText(_currentDisplaySurTeamType);
        CurrentHunTeamMajorText = GetMajorText(_currentDisplayHunTeamType);
        CurrentSurTeamTotalMinorScore = GetTotalMinorScore(_currentDisplaySurTeamType);
        CurrentHunTeamTotalMinorScore = GetTotalMinorScore(_currentDisplayHunTeamType);

        var (surWin, surTie) = GetMajorWinTie(_currentDisplaySurTeamType);
        CurrentSurTeamMajorWin = surWin;
        CurrentSurTeamMajorTie = surTie;

        var (hunWin, hunTie) = GetMajorWinTie(_currentDisplayHunTeamType);
        CurrentHunTeamMajorWin = hunWin;
        CurrentHunTeamMajorTie = hunTie;

        var currentGame = CurrentGameScore;
        var currentHalf = CurrentHalf;
        if (currentGame == null || currentHalf == null)
        {
            CurrentSurTeamMinorGameScoreText = "0";
            CurrentHunTeamMinorGameScoreText = "0";
            CurrentSurTeamMinorHalfScoreText = "-";
            CurrentHunTeamMinorHalfScoreText = "-";
            NotifyCurrentScoreObjectsChanged();
            return;
        }

        // 累计当前 Game 内从第一半到当前半场（含）的所有已记录半场小比分，按当前阵营映射。
        // 例：第一半 5:0、第二半录入平局 2:2 → 第二半显示 7:2；第二半未录入则显示 5:0。
        var halvesToAccumulate = currentHalf.HalfKind == ScoreHalfKind.SecondHalf
            ? currentGame.Halves
            : currentGame.Halves.Take(1);

        CurrentSurTeamMinorGameScoreText =
            FormatMinorScore(SumTeamMinorScore(halvesToAccumulate, _currentDisplaySurTeamType));
        CurrentHunTeamMinorGameScoreText =
            FormatMinorScore(SumTeamMinorScore(halvesToAccumulate, _currentDisplayHunTeamType));
        CurrentSurTeamMinorHalfScoreText =
            FormatMinorScore(GetTeamMinorScore(currentHalf, _currentDisplaySurTeamType, fallbackToZero: false));
        CurrentHunTeamMinorHalfScoreText =
            FormatMinorScore(GetTeamMinorScore(currentHalf, _currentDisplayHunTeamType, fallbackToZero: false));
        NotifyCurrentScoreObjectsChanged();
    }

    private void NotifyCurrentScoreObjectsChanged()
    {
        OnPropertyChanged(nameof(CurrentGameScore));
        OnPropertyChanged(nameof(CurrentHalf));
    }

    private static int SumTeamMinorScore(IEnumerable<ScoreHalf> halves, TeamType teamType)
    {
        var sum = 0;
        foreach (var half in halves)
        {
            var score = GetTeamMinorScore(half, teamType, fallbackToZero: true);
            if (score.HasValue)
                sum += score.Value;
        }
        return sum;
    }

    private static ScoreHalf CloneHalf(ScoreHalf half) =>
        new(
            half.Progress,
            half.HalfKind,
            half.Result,
            half.SurTeamTypeWhenRecorded,
            half.HunTeamTypeWhenRecorded);

    private static ObservableCollection<ScoreGame> CreateDefaultGames() =>
    [
        CreateScoreGame(1, ScoreGameKind.Normal, GameProgress.Game1FirstHalf, GameProgress.Game1SecondHalf),
        CreateScoreGame(2, ScoreGameKind.Normal, GameProgress.Game2FirstHalf, GameProgress.Game2SecondHalf),
        CreateScoreGame(3, ScoreGameKind.Normal, GameProgress.Game3FirstHalf, GameProgress.Game3SecondHalf),
        CreateScoreGame(3, ScoreGameKind.Overtime, GameProgress.Game3OvertimeFirstHalf,
            GameProgress.Game3OvertimeSecondHalf),
        CreateScoreGame(4, ScoreGameKind.Normal, GameProgress.Game4FirstHalf, GameProgress.Game4SecondHalf),
        CreateScoreGame(5, ScoreGameKind.Normal, GameProgress.Game5FirstHalf, GameProgress.Game5SecondHalf),
        CreateScoreGame(5, ScoreGameKind.Overtime, GameProgress.Game5OvertimeFirstHalf,
            GameProgress.Game5OvertimeSecondHalf)
    ];

    private static ScoreGame CreateScoreGame(
        int gameNumber,
        ScoreGameKind gameKind,
        GameProgress firstHalfProgress,
        GameProgress secondHalfProgress) =>
        new(
            new ScoreGameKey(gameNumber, gameKind),
            new ScoreHalf(firstHalfProgress, ScoreHalfKind.FirstHalf),
            new ScoreHalf(secondHalfProgress, ScoreHalfKind.SecondHalf));

    private static ScoreGameKey? ResolveScoreGameKey(GameProgress progress, bool isBo3Mode)
    {
        return (int)progress switch
        {
            0 or 1 => new ScoreGameKey(1, ScoreGameKind.Normal),
            2 or 3 => new ScoreGameKey(2, ScoreGameKind.Normal),
            4 or 5 => new ScoreGameKey(3, ScoreGameKind.Normal),
            6 or 7 => isBo3Mode
                ? new ScoreGameKey(3, ScoreGameKind.Overtime)
                : new ScoreGameKey(4, ScoreGameKind.Normal),
            8 or 9 => new ScoreGameKey(5, ScoreGameKind.Normal),
            10 or 11 => new ScoreGameKey(5, ScoreGameKind.Overtime),
            _ => null
        };
    }

    private static ScoreHalfKind? ResolveHalfKind(GameProgress progress)
    {
        return (int)progress switch
        {
            0 or 2 or 4 or 6 or 8 or 10 => ScoreHalfKind.FirstHalf,
            1 or 3 or 5 or 7 or 9 or 11 => ScoreHalfKind.SecondHalf,
            _ => null
        };
    }

    private string GetMajorText(TeamType teamType) =>
        teamType == TeamType.HomeTeam ? HomeMajorText : AwayMajorText;

    private (int win, int tie) GetMajorWinTie(TeamType teamType) =>
        teamType == TeamType.HomeTeam ? (HomeMajorWin, HomeMajorTie) : (AwayMajorWin, AwayMajorTie);

    private int GetTotalMinorScore(TeamType teamType) =>
        teamType == TeamType.HomeTeam ? HomeTotalMinorScore : AwayTotalMinorScore;

    private static int? GetTeamMinorScore(ScoreHalf half, TeamType teamType, bool fallbackToZero)
    {
        var score = teamType switch
        {
            TeamType.HomeTeam => half.HomeMinorScore,
            TeamType.AwayTeam => half.AwayMinorScore,
            _ => null
        };

        return score ?? (fallbackToZero ? 0 : null);
    }

    private static string FormatMajorText(int win, int tie) => $"W{win}  D{tie}";

    private static string FormatMinorScore(int? score) =>
        score?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private void SubscribeGames(ObservableCollection<ScoreGame> games)
    {
        games.CollectionChanged += OnGamesCollectionChanged;
        foreach (var game in games)
        {
            game.PropertyChanged += OnScoreGamePropertyChanged;
        }
    }

    private void UnsubscribeGames(ObservableCollection<ScoreGame> games)
    {
        games.CollectionChanged -= OnGamesCollectionChanged;
        foreach (var game in games)
        {
            game.PropertyChanged -= OnScoreGamePropertyChanged;
        }
    }

    private void OnGamesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems != null)
        {
            foreach (ScoreGame game in args.OldItems)
            {
                game.PropertyChanged -= OnScoreGamePropertyChanged;
            }
        }

        if (args.NewItems != null)
        {
            foreach (ScoreGame game in args.NewItems)
            {
                game.PropertyChanged += OnScoreGamePropertyChanged;
            }
        }

        Recalculate(_lastRecalculateIsBo3Mode);
    }

    private void OnScoreGamePropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        Recalculate(_lastRecalculateIsBo3Mode);
}
